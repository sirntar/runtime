// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Test.Common;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.XUnitExtensions;
using Xunit;
using Xunit.Abstractions;

namespace System.Net.Http.Functional.Tests
{
    using Configuration = System.Net.Test.Common.Configuration;

    public abstract class HttpClientHandlerTest_Headers : HttpClientHandlerTestBase
    {
        public HttpClientHandlerTest_Headers(ITestOutputHelper output) : base(output) { }

        private sealed class DerivedHttpHeaders : HttpHeaders { }

        [Fact]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/101115", typeof(PlatformDetection), nameof(PlatformDetection.IsFirefox))]
        public async Task SendAsync_RequestWithSimpleHeader_ResponseReferencesUnmodifiedRequestHeaders()
        {
            const string HeaderKey = "some-header-123", HeaderValue = "this is the expected header value";

            await LoopbackServerFactory.CreateClientAndServerAsync(async uri =>
            {
                using HttpClient client = CreateHttpClient();

                var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri) { Version = UseVersion };
                requestMessage.Headers.TryAddWithoutValidation(HeaderKey, HeaderValue);

                using HttpResponseMessage response = await client.SendAsync(TestAsync, requestMessage);
                Assert.Same(requestMessage, response.RequestMessage);
                Assert.Equal(HeaderValue, requestMessage.Headers.GetValues(HeaderKey).First());
            },
            async server =>
            {
                HttpRequestData requestData = await server.HandleRequestAsync(HttpStatusCode.OK);
                Assert.Equal(HeaderValue, requestData.GetSingleHeaderValue(HeaderKey));
            });
        }

        [Fact]
        [SkipOnPlatform(TestPlatforms.Browser, "User-Agent is not supported on Browser")]
        public async Task SendAsync_UserAgent_CorrectlyWritten()
        {
            string userAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_11_3) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/63.0.3239.18 Safari/537.36";

            await LoopbackServerFactory.CreateClientAndServerAsync(async uri =>
            {
                using (HttpClient client = CreateHttpClient())
                {
                    var message = new HttpRequestMessage(HttpMethod.Get, uri) { Version = UseVersion };
                    message.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    (await client.SendAsync(TestAsync, message).ConfigureAwait(false)).Dispose();
                }
            },
            async server =>
            {
                HttpRequestData requestData = await server.HandleRequestAsync(HttpStatusCode.OK);

                string agent = requestData.GetSingleHeaderValue("User-Agent");
                Assert.Equal(userAgent, agent);
            });
        }

        [Fact]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/101115", typeof(PlatformDetection), nameof(PlatformDetection.IsFirefox))]
        public async Task SendAsync_LargeHeaders_CorrectlyWritten()
        {
            if (UseVersion == HttpVersion.Version30)
            {
                // TODO: ActiveIssue
                return;
            }

            // Intentionally larger than 16K in total because that's the limit that will trigger a CONTINUATION frame in HTTP2.
            string largeHeaderValue = new string('a', 1024);
            int count = 20;

            await LoopbackServerFactory.CreateClientAndServerAsync(async uri =>
            {
                using HttpClient client = CreateHttpClient();

                var message = new HttpRequestMessage(HttpMethod.Get, uri) { Version = UseVersion };
                for (int i = 0; i < count; i++)
                {
                    message.Headers.TryAddWithoutValidation("large-header" + i, largeHeaderValue);
                }
                var response = await client.SendAsync(TestAsync, message).ConfigureAwait(false);
            },
            async server =>
            {
                HttpRequestData requestData = await server.HandleRequestAsync(HttpStatusCode.OK);

                for (int i = 0; i < count; i++)
                {
                    Assert.Equal(largeHeaderValue, requestData.GetSingleHeaderValue("large-header" + i));
                }
            });
        }

        [Fact]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/101115", typeof(PlatformDetection), nameof(PlatformDetection.IsFirefox))]
        public async Task SendAsync_DefaultHeaders_CorrectlyWritten()
        {
            const string Version = "2017-04-17";
            const string Blob = "BlockBlob";

            await LoopbackServerFactory.CreateClientAndServerAsync(async uri =>
            {
                using (HttpClient client = CreateHttpClient())
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation("x-ms-version", Version);
                    client.DefaultRequestHeaders.Add("x-ms-blob-type", Blob);
                    var message = new HttpRequestMessage(HttpMethod.Get, uri) { Version = UseVersion };
                    (await client.SendAsync(TestAsync, message).ConfigureAwait(false)).Dispose();
                }
            },
            async server =>
            {
                HttpRequestData requestData = await server.HandleRequestAsync(HttpStatusCode.OK);

                string headerValue = requestData.GetSingleHeaderValue("x-ms-blob-type");
                Assert.Equal(Blob, headerValue);
                headerValue = requestData.GetSingleHeaderValue("x-ms-version");
                Assert.Equal(Version, Version);
            });
        }

        [Theory]
        [InlineData("\u05D1\u05F1")]
        [InlineData("jp\u30A5")]
        [SkipOnPlatform(TestPlatforms.Browser, "Browser is relaxed about validating HTTP headers")]
        public async Task SendAsync_InvalidCharactersInHeader_Throw(string value)
        {
            await LoopbackServerFactory.CreateClientAndServerAsync(async uri =>
            {
                HttpClientHandler handler = CreateHttpClientHandler();
                using (HttpClient client = CreateHttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, uri) { Version = UseVersion };
                    Assert.True(request.Headers.TryAddWithoutValidation("bad", value));

                    await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(TestAsync, request));
                }

            },
            async server =>
            {
                try
                {
                    // Client should abort at some point so this is going to throw.
                    HttpRequestData requestData = await server.HandleRequestAsync(HttpStatusCode.OK).ConfigureAwait(false);
                }
                catch (Exception) { }
                ;
            });
        }

        [Theory]
        [InlineData("x-Special_name", "header name with underscore", true)] // underscores in header
        [InlineData("Date", "invaliddateformat", false)] // invalid format for header but added with TryAddWithoutValidation
        [InlineData("Accept-CharSet", "text/plain, text/json", false)] // invalid format for header but added with TryAddWithoutValidation
        [InlineData("Content-Location", "", false)] // invalid format for header but added with TryAddWithoutValidation
        [InlineData("Max-Forwards", "NotAnInteger", false)] // invalid format for header but added with TryAddWithoutValidation
        [ActiveIssue("https://github.com/dotnet/runtime/issues/101115", typeof(PlatformDetection), nameof(PlatformDetection.IsFirefox))]
        public async Task SendAsync_SpecialHeaderKeyOrValue_Success(string key, string value, bool parsable)
        {
            if (PlatformDetection.IsBrowser && (key == "Content-Location" || key == "Date" || key == "Accept-CharSet"))
            {
                // https://fetch.spec.whatwg.org/#forbidden-header-name
                return;
            }

            await LoopbackServerFactory.CreateClientAndServerAsync(async uri =>
            {
                bool contentHeader = false;
                using (HttpClient client = CreateHttpClient())
                {
                    var message = new HttpRequestMessage(HttpMethod.Get, uri) { Version = UseVersion };
                    if (!message.Headers.TryAddWithoutValidation(key, value))
                    {
                        message.Content = new StringContent("");
                        contentHeader = message.Content.Headers.TryAddWithoutValidation(key, value);
                    }
                    (await client.SendAsync(TestAsync, message).ConfigureAwait(false)).Dispose();
                }

                // Validate our test by validating our understanding of a header's parsability.
                HttpHeaders headers = contentHeader ? (HttpHeaders)
                    new StringContent("").Headers :
                    new HttpRequestMessage().Headers;
                if (parsable)
                {
                    headers.Add(key, value);
                }
                else
                {
                    Assert.Throws<FormatException>(() => headers.Add(key, value));
                }
            },
            async server =>
            {
                HttpRequestData requestData = await server.HandleRequestAsync(HttpStatusCode.OK);
                Assert.Equal(value, requestData.GetSingleHeaderValue(key));
            });
        }

        [Theory]
        [InlineData("Content-Security-Policy", 4618)]
        [InlineData("RandomCustomHeader", 12345)]
        public async Task GetAsync_LargeHeader_Success(string headerName, int headerValueLength)
        {
            var rand = new Random(42);
            string headerValue = new string(rand.GetItems<char>("ABCDEFGHIJKLMNOPQRSTUVWXYZ", headerValueLength));

            const string ContentString = "hello world";
            await LoopbackServerFactory.CreateClientAndServerAsync(async uri =>
            {
                using (HttpClient client = CreateHttpClient())
                using (HttpResponseMessage resp = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead))
                {
                    Assert.Equal(headerValue, resp.Headers.GetValues(headerName).Single());
                    Assert.Equal(ContentString, await resp.Content.ReadAsStringAsync());
                }
            },
            async server =>
            {
                var headers = new List<HttpHeaderData>();
                headers.Add(new HttpHeaderData(headerName, headerValue));
                await server.HandleRequestAsync(HttpStatusCode.OK, headers: headers, content: ContentString);
            });
        }

        [Fact]
        public async Task GetAsync_EmptyResponseHeader_Success()
        {
            IList<HttpHeaderData> headers = new HttpHeaderData[] {
                                                new HttpHeaderData("Date", $"{DateTimeOffset.UtcNow:R}"),
                                                new HttpHeaderData("x-empty", ""),
                                                new HttpHeaderData("x-last", "bye") };

            await LoopbackServerFactory.CreateClientAndServerAsync(async uri =>
            {
                using (HttpClient client = CreateHttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(uri).ConfigureAwait(false);
                    // browser sends more headers
                    if (PlatformDetection.IsNotBrowser)
                    {
                        Assert.Equal(headers.Count, response.Headers.Count());
                    }
                    Assert.NotNull(response.Headers.GetValues("x-empty"));
                }
            },
            async server =>
            {
                await server.AcceptConnectionAsync(async connection =>
                {
                    await connection.ReadRequestDataAsync();
                    await connection.SendResponseAsync(HttpStatusCode.OK, headers);
                });
            });
        }

        [Fact]
        public async Task GetAsync_MissingExpires_ReturnNull()
        {
            await LoopbackServerFactory.CreateClientAndServerAsync(async uri =>
            {
                using (HttpClient client = CreateHttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(uri);
                    Assert.Null(response.Content.Headers.Expires);
                }
            },
           async server =>
           {
               await server.HandleRequestAsync(HttpStatusCode.OK);
           });
        }

        [Theory]
        [InlineData("Thu, 01 Dec 1994 16:00:00 GMT", true)]
        [InlineData("-1", false)]
        [InlineData("0", false)]
        public async Task SendAsync_Expires_Success(string value, bool isValid)
        {
            await LoopbackServerFactory.CreateClientAndServerAsync(async uri =>
            {
                using (HttpClient client = CreateHttpClient())
                {
                    var message = new HttpRequestMessage(HttpMethod.Get, uri) { Version = UseVersion };
                    HttpResponseMessage response = await client.SendAsync(TestAsync, message);
                    Assert.NotNull(response.Content.Headers.Expires);
                    // Invalid date should be converted to MinValue so everything is expired.
                    Assert.Equal(isValid ? DateTime.Parse(value) : DateTimeOffset.MinValue, response.Content.Headers.Expires);
                }
            },
            async server =>
            {
                IList<HttpHeaderData> headers = new HttpHeaderData[] { new HttpHeaderData("Expires", value) };

                HttpRequestData requestData = await server.HandleRequestAsync(HttpStatusCode.OK, headers);
            });
        }

        [Theory]
        [InlineData("-1", false)]
        [InlineData("Thu, 01 Dec 1994 16:00:00 GMT", true)]
        public void HeadersAdd_CustomExpires_Success(string value, bool isValid)
        {
            var headers = new DerivedHttpHeaders();
            if (!isValid)
            {
                Assert.Throws<FormatException>(() => headers.Add("Expires", value));
            }
            Assert.True(headers.TryAddWithoutValidation("Expires", value));
            Assert.Equal(1, Enumerable.Count(headers.GetValues("Expires")));
            Assert.Equal(value, headers.GetValues("Expires").First());
        }

        [Theory]
        [InlineData("Accept-Encoding", "identity,gzip")]
        public async Task SendAsync_RequestHeaderInResponse_Success(string name, string value)
        {
            await LoopbackServerFactory.CreateClientAndServerAsync(async uri =>
            {
                using (HttpClient client = CreateHttpClient())
                {
                    var message = new HttpRequestMessage(HttpMethod.Get, uri) { Version = UseVersion };
                    HttpResponseMessage response = await client.SendAsync(TestAsync, message);

                    Assert.Equal(value, response.Headers.GetValues(name).First());
                }
            },
            async server =>
            {
                IList<HttpHeaderData> headers = new HttpHeaderData[] { new HttpHeaderData(name, value) };

                HttpRequestData requestData = await server.HandleRequestAsync(HttpStatusCode.OK, headers);
            });
        }

        [OuterLoop("Uses external servers", typeof(PlatformDetection), nameof(PlatformDetection.LocalEchoServerIsNotAvailable))]
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/101115", typeof(PlatformDetection), nameof(PlatformDetection.IsFirefox))]
        public async Task SendAsync_GetWithValidHostHeader_Success(bool withPort)
        {
            if (UseVersion == HttpVersion.Version30)
            {
                // External servers do not support HTTP3 currently.
                return;
            }
            if (PlatformDetection.LocalEchoServerIsAvailable && !withPort)
            {
                // we always have custom port with the local echo server, so we couldn't test without it
                return;
            }

            var m = new HttpRequestMessage(HttpMethod.Get, Configuration.Http.SecureRemoteEchoServer) { Version = UseVersion };
            m.Headers.Host = !PlatformDetection.LocalEchoServerIsAvailable && withPort
                                ? Configuration.Http.SecureHost + ":" + Configuration.Http.SecurePort
                                : Configuration.Http.SecureHost;

            using (HttpClient client = CreateHttpClient())
            using (HttpResponseMessage response = await client.SendAsync(TestAsync, m))
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                _output.WriteLine(responseContent);
                TestHelper.VerifyResponseBody(
                    responseContent,
                    response.Content.Headers.ContentMD5,
                    false,
                    null);
            }
        }

        [OuterLoop("Uses external servers", typeof(PlatformDetection), nameof(PlatformDetection.LocalEchoServerIsNotAvailable))]
        [Fact]
        [SkipOnPlatform(TestPlatforms.Browser, "Not supported on Browser")]
        public async Task SendAsync_GetWithInvalidHostHeader_ThrowsException()
        {
            if (LoopbackServerFactory.Version >= HttpVersion.Version20)
            {
                // Only SocketsHttpHandler with HTTP/1.x uses the Host header to influence the SSL auth.
                // Host header is not used for HTTP2 and later.
                return;
            }

            var m = new HttpRequestMessage(HttpMethod.Get, Configuration.Http.SecureRemoteEchoServer) { Version = UseVersion };
            m.Headers.Host = "hostheaderthatdoesnotmatch";

            using (HttpClient client = CreateHttpClient(CreateHttpClientHandler(allowAllCertificates: false)))
            {
                await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(TestAsync, m));
            }
        }

        [Fact]
        [SkipOnPlatform(TestPlatforms.Browser, "Browser is relaxed about validating HTTP headers")]
        public async Task SendAsync_WithZeroLengthHeaderName_Throws()
        {
            await LoopbackServerFactory.CreateClientAndServerAsync(
                async uri =>
                {
                    using HttpClient client = CreateHttpClient();
                    await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(uri));
                },
                async server =>
                {
                    // The client may detect the bad header and close the connection before we are done sending the response.
                    // So, eat any IOException that occurs here.
                    try
                    {
                        await server.HandleRequestAsync(headers: new[]
                        {
                            new HttpHeaderData("", "foo")
                        });
                    }
                    catch (IOException) { }
                });
        }

        private static readonly (string Name, Encoding ValueEncoding, string Separator, string[] Values)[] s_nonAsciiHeaders = new[]
        {
            ("foo",             Encoding.ASCII,     ", ", new[] { "bar" }),
            ("header-0",        Encoding.UTF8,      ", ", new[] { "\uD83D\uDE03", "\uD83D\uDE48\uD83D\uDE49\uD83D\uDE4A" }),
            ("Cache-Control",   Encoding.UTF8,      ", ", new[] { "no-cache" }),
            ("header-1",        Encoding.UTF8,      ", ", new[] { "\uD83D\uDE03" }),
            ("Some-Header1",    Encoding.Latin1,    ", ", new[] { "\uD83D\uDE03", "UTF8-best-fit-to-latin1" }),
            ("Some-Header2",    Encoding.Latin1,    ", ", new[] { "\u00FF", "\u00C4nd", "Ascii\u00A9" }),
            ("Some-Header3",    Encoding.ASCII,     ", ", new[] { "\u00FF", "\u00C4nd", "Ascii\u00A9", "Latin1-best-fit-to-ascii" }),
            ("header-2",        Encoding.UTF8,      ", ", new[] { "\uD83D\uDE48\uD83D\uDE49\uD83D\uDE4A" }),
            ("header-3",        Encoding.UTF8,      ", ", new[] { "\uFFFD" }),
            ("header-4",        Encoding.UTF8,      ", ", new[] { "\uD83D\uDE48\uD83D\uDE49\uD83D\uDE4A", "\uD83D\uDE03" }),
            ("Cookie",          Encoding.UTF8,      "; ", new[] { "Cookies", "\uD83C\uDF6A", "everywhere" }),
            ("Set-Cookie",      Encoding.UTF8,      ", ", new[] { "\uD83C\uDDF8\uD83C\uDDEE" }),
            ("header-5",        Encoding.UTF8,      ", ", new[] { "\uD83D\uDE48\uD83D\uDE49\uD83D\uDE4A", "foo", "\uD83D\uDE03", "bar" }),
            ("bar",             Encoding.UTF8,      ", ", new[] { "foo" }),
            ("Location",        Encoding.Latin1,    ", ", new[] { "\u00D0\u00A4" })
        };

        [Fact]
        [SkipOnPlatform(TestPlatforms.Browser, "Socket is not supported on Browser")]
        public async Task SendAsync_CustomRequestEncodingSelector_CanSendNonAsciiHeaderValues()
        {
            await LoopbackServerFactory.CreateClientAndServerAsync(
                async uri =>
                {
                    var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri)
                    {
                        Version = UseVersion
                    };

                    foreach ((string name, _, _, string[] values) in s_nonAsciiHeaders)
                    {
                        requestMessage.Headers.Add(name, values);
                    }

                    List<string> seenHeaderNames = new List<string>();

                    using HttpClientHandler handler = CreateHttpClientHandler();
                    var underlyingHandler = (SocketsHttpHandler)GetUnderlyingSocketsHttpHandler(handler);

                    underlyingHandler.RequestHeaderEncodingSelector = (name, request) =>
                    {
                        Assert.NotNull(name);
                        Assert.Same(request, requestMessage);
                        seenHeaderNames.Add(name);
                        return Assert.Single(s_nonAsciiHeaders, h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ValueEncoding;
                    };

                    using HttpClient client = CreateHttpClient(handler);

                    await client.SendAsync(TestAsync, requestMessage);

                    foreach ((string name, _, _, _) in s_nonAsciiHeaders)
                    {
                        Assert.Contains(name, seenHeaderNames);
                    }
                },
                async server =>
                {
                    HttpRequestData requestData = await server.HandleRequestAsync();

                    Assert.All(requestData.Headers,
                        h => Assert.False(h.HuffmanEncoded, "Expose raw decoded bytes once HuffmanEncoding is supported"));

                    foreach ((string name, Encoding valueEncoding, string separator, string[] values) in s_nonAsciiHeaders)
                    {
                        byte[] valueBytes = valueEncoding.GetBytes(string.Join(separator, values));
                        Assert.Single(requestData.Headers,
                            h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && h.Raw.AsSpan().IndexOf(valueBytes) != -1);
                    }
                });
        }

        [Fact]
        [SkipOnPlatform(TestPlatforms.Browser, "Socket is not supported on Browser")]
        public async Task SendAsync_CustomResponseEncodingSelector_CanReceiveNonAsciiHeaderValues()
        {
            await LoopbackServerFactory.CreateClientAndServerAsync(
                async uri =>
                {
                    var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri)
                    {
                        Version = UseVersion
                    };

                    List<string> seenHeaderNames = new List<string>();

                    using HttpClientHandler handler = CreateHttpClientHandler();
                    var underlyingHandler = (SocketsHttpHandler)GetUnderlyingSocketsHttpHandler(handler);

                    underlyingHandler.ResponseHeaderEncodingSelector = (name, request) =>
                    {
                        Assert.NotNull(name);
                        Assert.Same(request, requestMessage);
                        seenHeaderNames.Add(name);

                        if (s_nonAsciiHeaders.Any(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                        {
                            return Assert.Single(s_nonAsciiHeaders, h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ValueEncoding;
                        }

                        // Not one of our custom headers
                        return null;
                    };

                    using HttpClient client = CreateHttpClient(handler);

                    using HttpResponseMessage response = await client.SendAsync(TestAsync, requestMessage);

                    foreach ((string name, Encoding valueEncoding, string separator, string[] values) in s_nonAsciiHeaders)
                    {
                        Assert.Contains(name, seenHeaderNames);
                        IEnumerable<string> receivedValues = Assert.Single(response.Headers.NonValidated, h => h.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
                        string value = Assert.Single(receivedValues);

                        string expected = valueEncoding.GetString(valueEncoding.GetBytes(string.Join(separator, values)));
                        Assert.Equal(expected, value, StringComparer.OrdinalIgnoreCase);
                    }
                },
                async server =>
                {
                    List<HttpHeaderData> headerData = s_nonAsciiHeaders
                        .Select(h => new HttpHeaderData(h.Name, string.Join(h.Separator, h.Values), valueEncoding: h.ValueEncoding))
                        .ToList();

                    await server.HandleRequestAsync(headers: headerData);
                });
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsNotNodeJS))]
        public async Task SendAsync_ContentLengthAndTransferEncodingHeaders_IgnoreContentLength()
        {
            await LoopbackServer.CreateServerAsync(async (server, uri) =>
            {
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, uri)
                {
                    Version = UseVersion
                };
                using HttpClient client = new HttpClient();
                Task<HttpResponseMessage> getResponse = client.SendAsync(requestMessage);
                await server.AcceptConnectionAsync(async connection =>
                {
                    await connection.SendResponseAsync(HttpStatusCode.OK,
                        new List<HttpHeaderData>
                        {
                            new HttpHeaderData("Content-Length", "33"),
                            new HttpHeaderData("Transfer-Encoding", "chunked")
                        }, "5\r\nhello\r\n5\r\nworld\r\n3\r\nyay\r\n0\r\n\r\n");

                    using (HttpResponseMessage response = await getResponse)
                    {
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                        Assert.True(response.Headers.Contains("Transfer-Encoding"));
                        Assert.Equal("chunked", Assert.Single(response.Headers.GetValues("Transfer-Encoding")));
                        Assert.Equal(33, response.Content.Headers.ContentLength);

                        string content = await response.Content.ReadAsStringAsync();
                        Assert.Equal("helloworldyay", content);
                    }
                });
            });
        }

        [ConditionalTheory(typeof(PlatformDetection), nameof(PlatformDetection.IsNotBrowser))]
        [InlineData(false, "test\nxwow\nmore\n", false)]
        [InlineData(false, "test\rwow\rmore\r\n", false)]
        [InlineData(true, "one\0two\0three\0", false)]
        [InlineData(false, "test\nxwow\nmore\n", true)]
        [InlineData(false, "test\rwow\rmore\r\n", true)]
        [InlineData(true, "one\0two\0three\0", true)]
        public async Task SendAsync_InvalidCharactersInResponseHeader_ReplacedWithSpaces(bool testHttp11, string value, bool testTrailers)
        {
            if (!testHttp11 && UseVersion == HttpVersion.Version11)
            {
                throw new SkipTestException("This case is not valid for HTTP 1.1");
            }

            string expectedValue = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\0', ' ');
            await LoopbackServerFactory.CreateClientAndServerAsync(
                async uri =>
                {
                    using HttpClient client = CreateHttpClient();

                    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri)
                    {
                        Version = UseVersion,
                        VersionPolicy = HttpVersionPolicy.RequestVersionExact
                    };

                    using HttpResponseMessage response = await client.SendAsync(request);
                    HttpResponseHeaders headerCollection = testTrailers ? response.TrailingHeaders : response.Headers;
                    Assert.Equal(expectedValue, headerCollection.GetValues("test").Single());
                },
                async server =>
                {
                    List<HttpHeaderData>? headers = testTrailers ? null : [new HttpHeaderData("test", value)];
                    List<HttpHeaderData>? trailers = testTrailers ? [new HttpHeaderData("test", value)] : null;
                    string content = "hello";

                    if (testTrailers && UseVersion == HttpVersion.Version11)
                    {
                        headers = [new HttpHeaderData("Transfer-Encoding", "chunked")];
                        content = $"{content.Length:X}\r\n{content}\r\n";
                    }

                    await server.AcceptConnectionAsync(async connection =>
                    {
                        await connection.ReadRequestDataAsync();
                        await connection.SendResponseAsync(headers: headers, content: content, isFinal: trailers is null);
                        if (trailers is { })
                        {
                            await connection.SendResponseHeadersAsync(headers: trailers, isTrailingHeader: true);
                        }
                    });
                });
        }
    }
}
