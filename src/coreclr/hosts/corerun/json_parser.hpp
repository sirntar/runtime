// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef __JSON_PARSER_HPP__
#define __JSON_PARSER_HPP__

#include <vector>
#include <string>

namespace corefx_json
{
#ifdef TARGET_WINDOWS
    using char_t = wchar_t;
    using string_t = std::basic_string<char_t>;
    using stringstream_t = std::basic_stringstream<char_t>;
    using string_utf8_t = std::basic_string<char>;
#else
    using char_t = char;
    using string_t = std::basic_string<char_t>;
    using stringstream_t = std::basic_stringstream<char_t>;
    using string_utf8_t = std::basic_string<char>;
#endif

    enum class json_value_t {
        J_INVALID = -1,
        J_FALSE = 0,
        J_TRUE = 1,
        J_NULL = 2,
        J_NUMBER = 3,
        J_STRING = 4,
        J_ARRAY = 5,
        J_OBJECT = 6,
        J_ANY = 7
    };

    class json_parser_t final
    {
        std::vector<char_t*> keys;
        std::vector<char_t*> values;

    private:
        void parse_line(char start, char end, size_t max_len)
        {

        }

    public:
        void parse(const std::string& json_string)
        {

        }
    };
}

#endif // __JSON_PARSER_HPP__
