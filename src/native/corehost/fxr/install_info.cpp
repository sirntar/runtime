// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "install_info.h"
#include "pal.h"
#include "trace.h"
#include "utils.h"

bool install_info::print_environment(const pal::char_t* leading_whitespace)
{
    bool found_any = false;

    const pal::char_t* fmt = _X("%s%-17s [%s]");
    pal::string_t value;
    if (pal::getenv(DOTNET_ROOT_ENV_VAR, &value))
    {
        found_any = true;
        trace::println(fmt, leading_whitespace, DOTNET_ROOT_ENV_VAR, value.c_str());
    }

    for (uint32_t i = 0; i < static_cast<uint32_t>(pal::architecture::__last); ++i)
    {
        pal::string_t env_var = get_dotnet_root_env_var_for_arch(static_cast<pal::architecture>(i));
        if (pal::getenv(env_var.c_str(), &value))
        {
            found_any = true;
            trace::println(fmt, leading_whitespace, env_var.c_str(), value.c_str());
        }
    }

    return found_any;
}

bool install_info::try_get_install_location(pal::architecture arch, pal::string_t& out_install_location, bool* out_is_registered)
{
    pal::string_t install_location;
    bool is_registered = pal::get_dotnet_self_registered_dir_for_arch(arch, &install_location);
    bool found = is_registered
        || (pal::get_default_installation_dir_for_arch(arch, &install_location) && pal::directory_exists(install_location));
    if (!found)
        return false;

    remove_trailing_dir_separator(&install_location);
    out_install_location = install_location;
    if (out_is_registered != nullptr)
        *out_is_registered = is_registered;

    return true;
}

bool install_info::enumerate_other_architectures(std::function<void(pal::architecture, const pal::string_t&, bool)> callback)
{
    bool found_any = false;
    for (uint32_t i = 0; i < static_cast<uint32_t>(pal::architecture::__last); ++i)
    {
        pal::architecture arch = static_cast<pal::architecture>(i);
        if (arch == get_current_arch())
            continue;

        bool is_registered;
        pal::string_t install_location;
        if (try_get_install_location(arch, install_location, &is_registered))
        {
            found_any = true;
            callback(arch, install_location, is_registered);
        }
    }

    return found_any;
}

bool install_info::print_other_architectures(const pal::char_t* leading_whitespace)
{
    return enumerate_other_architectures(
        [&](pal::architecture arch, const pal::string_t& install_location, bool is_registered)
        {
            trace::println(_X("%s%-5s [%s]"), leading_whitespace, get_arch_name(arch), install_location.c_str());
            if (is_registered)
            {
                trace::println(_X("%s  registered at [%s]"), leading_whitespace, pal::get_dotnet_self_registered_config_location(arch).c_str());
            }
        });
}
