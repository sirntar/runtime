// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace System.IO.Pipes
{
    /// <summary>
    /// Named pipe server
    /// </summary>
    public sealed partial class NamedPipeServerStream : PipeStream
    {
        private ConnectionValueTaskSource? _reusableConnectionValueTaskSource; // reusable ConnectionValueTaskSource that is currently NOT being used

        internal NamedPipeServerStream(
            string pipeName,
            PipeDirection direction,
            int maxNumberOfServerInstances,
            PipeTransmissionMode transmissionMode,
            PipeOptions options,
            int inBufferSize,
            int outBufferSize,
            PipeSecurity? pipeSecurity,
            HandleInheritability inheritability = HandleInheritability.None,
            PipeAccessRights additionalAccessRights = default)
            : base(direction, transmissionMode, outBufferSize)
        {
            ValidateParameters(pipeName, direction, maxNumberOfServerInstances, transmissionMode, options, inBufferSize, outBufferSize, inheritability);

            if (pipeSecurity != null && IsCurrentUserOnly)
            {
                throw new ArgumentException(SR.NotSupported_PipeSecurityIsCurrentUserOnly, nameof(pipeSecurity));
            }

            Create(pipeName, direction, maxNumberOfServerInstances, transmissionMode, options, inBufferSize, outBufferSize, pipeSecurity, inheritability, additionalAccessRights);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                Interlocked.Exchange(ref _reusableConnectionValueTaskSource, null)?.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        internal override void TryToReuse(PipeValueTaskSource source)
        {
            base.TryToReuse(source);

            if (source is ConnectionValueTaskSource connectionSource)
            {
                if (Interlocked.CompareExchange(ref _reusableConnectionValueTaskSource, connectionSource, null) is not null)
                {
                    source.Dispose();
                }
                else if (State == PipeState.Closed)
                {
                    Interlocked.Exchange(ref _reusableConnectionValueTaskSource, null)?.Dispose();
                }
            }
        }

        private void Create(string pipeName, PipeDirection direction, int maxNumberOfServerInstances,
                PipeTransmissionMode transmissionMode, PipeOptions options, int inBufferSize, int outBufferSize,
                HandleInheritability inheritability)
        {
            Create(pipeName, direction, maxNumberOfServerInstances, transmissionMode, options, inBufferSize,
                outBufferSize, null, inheritability, 0);
        }

        // This overload is used in Mono to implement public constructors.
        private void Create(string pipeName, PipeDirection direction, int maxNumberOfServerInstances,
                PipeTransmissionMode transmissionMode, PipeOptions options, int inBufferSize, int outBufferSize,
                PipeSecurity? pipeSecurity, HandleInheritability inheritability, PipeAccessRights additionalAccessRights)
        {
            Debug.Assert(!string.IsNullOrEmpty(pipeName), "fullPipeName is null or empty");
            Debug.Assert(direction >= PipeDirection.In && direction <= PipeDirection.InOut, "invalid pipe direction");
            Debug.Assert(inBufferSize >= 0, "inBufferSize is negative");
            Debug.Assert(outBufferSize >= 0, "outBufferSize is negative");
            Debug.Assert((maxNumberOfServerInstances >= 1 && maxNumberOfServerInstances <= 254) || (maxNumberOfServerInstances == MaxAllowedServerInstances), "maxNumberOfServerInstances is invalid");
            Debug.Assert(transmissionMode >= PipeTransmissionMode.Byte && transmissionMode <= PipeTransmissionMode.Message, "transmissionMode is out of range");

            string fullPipeName = Path.GetFullPath(@"\\.\pipe\" + pipeName);

            // Make sure the pipe name isn't one of our reserved names for anonymous pipes.
            if (string.Equals(fullPipeName, @"\\.\pipe\anonymous", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentOutOfRangeException(nameof(pipeName), SR.ArgumentOutOfRange_AnonymousReserved);
            }

            if (IsCurrentUserOnly)
            {
                Debug.Assert(pipeSecurity == null);

                using (WindowsIdentity currentIdentity = WindowsIdentity.GetCurrent())
                {
                    SecurityIdentifier identifier = currentIdentity.Owner!;

                    // Grant full control to the owner so multiple servers can be opened.
                    // Full control is the default per MSDN docs for CreateNamedPipe.
                    PipeAccessRule rule = new PipeAccessRule(identifier, PipeAccessRights.FullControl, AccessControlType.Allow);
                    pipeSecurity = new PipeSecurity();

                    pipeSecurity.AddAccessRule(rule);
                    pipeSecurity.SetOwner(identifier);
                }

                // PipeOptions.CurrentUserOnly is special since it doesn't match directly to a corresponding Win32 valid flag.
                // Remove it, while keeping others untouched since historically this has been used as a way to pass flags to CreateNamedPipe
                // that were not defined in the enumeration.
                options &= ~PipeOptions.CurrentUserOnly;
            }

            int openMode = ((int)direction) |
                           (maxNumberOfServerInstances == 1 ? Interop.Kernel32.FileOperations.FILE_FLAG_FIRST_PIPE_INSTANCE : 0) |
                           (int)options |
                           (int)additionalAccessRights;

            // We automatically set the ReadMode to match the TransmissionMode.
            int pipeModes = (int)transmissionMode << 2 | (int)transmissionMode << 1;

            // Convert -1 to 255 to match win32 (we asserted that it is between -1 and 254).
            if (maxNumberOfServerInstances == MaxAllowedServerInstances)
            {
                maxNumberOfServerInstances = 255;
            }

            GCHandle pinningHandle = default;
            try
            {
                Interop.Kernel32.SECURITY_ATTRIBUTES secAttrs = GetSecAttrs(inheritability, pipeSecurity, ref pinningHandle);
                SafePipeHandle handle = Interop.Kernel32.CreateNamedPipe(fullPipeName, openMode, pipeModes,
                    maxNumberOfServerInstances, outBufferSize, inBufferSize, 0, ref secAttrs);

                if (handle.IsInvalid)
                {
                    Exception e = Win32Marshal.GetExceptionForLastWin32Error();
                    handle.Dispose();
                    throw e;
                }

                InitializeHandle(handle, false, (options & PipeOptions.Asynchronous) != 0);
            }
            finally
            {
                if (pinningHandle.IsAllocated)
                {
                    pinningHandle.Free();
                }
            }
        }

        // This will wait until the client calls Connect().  If we return from this method, we guarantee that
        // the client has returned from its Connect call.   The client may have done so before this method
        // was called (but not before this server is been created, or, if we were servicing another client,
        // not before we called Disconnect), in which case, there may be some buffer already in the pipe waiting
        // for us to read.  See NamedPipeClientStream.Connect for more information.
        public void WaitForConnection()
        {
            CheckConnectOperationsServerWithHandle();

            if (IsAsync)
            {
                WaitForConnectionCoreAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
            }
            else
            {
                if (!Interop.Kernel32.ConnectNamedPipe(InternalHandle!, IntPtr.Zero))
                {
                    int errorCode = Marshal.GetLastPInvokeError();

                    if (errorCode != Interop.Errors.ERROR_PIPE_CONNECTED)
                    {
                        throw Win32Marshal.GetExceptionForWin32Error(errorCode);
                    }

                    // pipe already connected
                    if (State == PipeState.Connected)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_PipeAlreadyConnected);
                    }

                    // If we reach here then a connection has been established.  This can happen if a client
                    // connects in the interval between the call to CreateNamedPipe and the call to ConnectNamedPipe.
                    // In this situation, there is still a good connection between client and server, even though
                    // ConnectNamedPipe returns zero.
                }

                State = PipeState.Connected;
            }
        }

        public Task WaitForConnectionAsync(CancellationToken cancellationToken) =>
            cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) :
            IsAsync ? WaitForConnectionCoreAsync(cancellationToken).AsTask() :
            AsyncOverSyncWithIoCancellation.InvokeAsync(static s => s.WaitForConnection(), this, cancellationToken).AsTask();

        public void Disconnect()
        {
            CheckDisconnectOperations();

            // Disconnect the pipe.
            if (!Interop.Kernel32.DisconnectNamedPipe(InternalHandle!))
            {
                throw Win32Marshal.GetExceptionForLastWin32Error();
            }

            State = PipeState.Disconnected;
        }

        // Gets the username of the connected client.  Note that we will not have access to the client's
        // username until it has written at least once to the pipe (and has set its impersonationLevel
        // argument appropriately).
        public unsafe string GetImpersonationUserName()
        {
            CheckWriteOperations();

            const uint UserNameMaxLength = Interop.Kernel32.CREDUI_MAX_USERNAME_LENGTH + 1;
            char* userName = stackalloc char[(int)UserNameMaxLength]; // ~1K

            if (Interop.Kernel32.GetNamedPipeHandleStateW(InternalHandle!, null, null, null, null, userName, UserNameMaxLength))
            {
                return new string(userName);
            }

            return HandleGetImpersonationUserNameError(Marshal.GetLastPInvokeError(), UserNameMaxLength, userName);
        }

        // This method calls a delegate while impersonating the client. Note that we will not have
        // access to the client's security token until it has written at least once to the pipe
        // (and has set its impersonationLevel argument appropriately).
        public void RunAsClient(PipeStreamImpersonationWorker impersonationWorker)
        {
            CheckWriteOperations();
            ExecuteHelper execHelper = new ExecuteHelper(impersonationWorker, InternalHandle);

            try
            {
                ImpersonateAndTryCode(execHelper);
            }
            finally
            {
                RevertImpersonationOnBackout(execHelper);
            }

            // now handle win32 impersonate/revert specific errors by throwing corresponding exceptions
            if (execHelper._impersonateErrorCode != 0)
            {
                throw WinIOError(execHelper._impersonateErrorCode);
            }
            else if (execHelper._revertImpersonateErrorCode != 0)
            {
                throw WinIOError(execHelper._revertImpersonateErrorCode);
            }
        }

        private static void ImpersonateAndTryCode(object? helper)
        {
            ExecuteHelper execHelper = (ExecuteHelper)helper!;

            if (Interop.Advapi32.ImpersonateNamedPipeClient(execHelper._handle!))
            {
                execHelper._mustRevert = true;
            }
            else
            {
                execHelper._impersonateErrorCode = Marshal.GetLastPInvokeError();
            }

            if (execHelper._mustRevert)
            {
                // impersonate passed so run user code
                execHelper._userCode();
            }
        }

        private static void RevertImpersonationOnBackout(object? helper)
        {
            ExecuteHelper execHelper = (ExecuteHelper)helper!;

            if (execHelper._mustRevert)
            {
                if (!Interop.Advapi32.RevertToSelf())
                {
                    execHelper._revertImpersonateErrorCode = Marshal.GetLastPInvokeError();
                }
            }
        }

        internal sealed class ExecuteHelper
        {
            internal PipeStreamImpersonationWorker _userCode;
            internal SafePipeHandle? _handle;
            internal bool _mustRevert;
            internal int _impersonateErrorCode;
            internal int _revertImpersonateErrorCode;

            internal ExecuteHelper(PipeStreamImpersonationWorker userCode, SafePipeHandle? handle)
            {
                _userCode = userCode;
                _handle = handle;
            }
        }

        // Async version of WaitForConnection.  See the comments above for more info.
        private unsafe ValueTask WaitForConnectionCoreAsync(CancellationToken cancellationToken)
        {
            CheckConnectOperationsServerWithHandle();
            Debug.Assert(IsAsync);

            ConnectionValueTaskSource? vts = Interlocked.Exchange(ref _reusableConnectionValueTaskSource, null) ?? new ConnectionValueTaskSource(this);
            try
            {
                vts.PrepareForOperation();
                if (!Interop.Kernel32.ConnectNamedPipe(InternalHandle!, vts._overlapped))
                {
                    int errorCode = Marshal.GetLastPInvokeError();
                    switch (errorCode)
                    {
                        case Interop.Errors.ERROR_IO_PENDING:
                            // Common case: IO was initiated, completion will be handled by callback.
                            // Register for cancellation now that the operation has been initiated.
                            vts.RegisterForCancellation(cancellationToken);
                            break;

                        case Interop.Errors.ERROR_PIPE_CONNECTED:
                            // If we are here then the pipe is already connected.
                            // IOCompletitionCallback will not be called because we completed synchronously.
                            vts.Dispose();
                            if (State == PipeState.Connected)
                            {
                                return ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException(SR.InvalidOperation_PipeAlreadyConnected)));
                            }
                            State = PipeState.Connected;
                            return ValueTask.CompletedTask;

                        default:
                            vts.Dispose();
                            return ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(Win32Marshal.GetExceptionForWin32Error(errorCode)));
                    }
                }
            }
            catch
            {
                vts.Dispose();
                throw;
            }

            // Completion handled by callback.
            vts.FinishedScheduling();
            return new ValueTask(vts, vts.Version);
        }

        private void CheckConnectOperationsServerWithHandle()
        {
            if (InternalHandle == null)
            {
                throw new InvalidOperationException(SR.InvalidOperation_PipeHandleNotSet);
            }
            CheckConnectOperationsServer();
        }
    }
}
