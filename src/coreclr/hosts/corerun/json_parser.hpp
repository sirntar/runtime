// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef __JSON_PARSER_HPP__
#define __JSON_PARSER_HPP__

#include <cassert>

#include <vector>
#include <string>

#include <unordered_map>
#include <algorithm>

#include <memory>

#include <fstream>
#include <iostream>

namespace corefx_json
{
    using string_t = std::basic_string<char>;

    class json_namespace_t final 
    {
        std::vector<string_t> keys;
        std::vector<string_t> values;

        string_t namespace_name = "global";

    public:
        json_namespace_t() = default;

        void SetNamespace(const string_t& name)
        {
            namespace_name = name;
        }

        [[nodiscard]] string_t Namespace() const
        {
            return namespace_name;
        }

        void Add(string_t key, string_t value)
        {
            keys.push_back(key);
            values.push_back(value);
        }

        void Delete(string_t key)
        {
            auto it = std::find(keys.begin(), keys.end(), key);

            if (it !=  keys.end())
            {
                auto it_v = std::next(values.begin(), std::distance(keys.begin(), it));
                values.erase(it_v);
                keys.erase(it);
            }
        }

        void clear()
        {
            keys.clear();
            values.clear();
        }

        [[nodiscard]] string_t get(const string_t& key) const
        {
            for (size_t i = 0; i < keys.size(); i++)
            {
                if (keys.at(i) == key)
                {
                    return values.at(i);
                }
            }
            return "(null)";
        }

        [[nodiscard]] string_t operator[](const string_t& key) const
        {
            return get(key);
        }

        [[nodiscard]] size_t size() const
        {
            return keys.size();
        }

        [[nodiscard]] string_t at(const int& index) const
        {
            if (index < size())
            {
                return values[index];
            }
            return "(null)";
        }

        [[nodiscard]] string_t operator[](const int& index) const
        {
            return this->at(index);
        }

        [[nodiscard]] string_t key_at(const int& index) const
        {
            if (index < size())
            {
                return keys[index];
            }
            return "(null)";
        }

        [[nodiscard]] std::vector<string_t>::const_iterator keys_begin() const
        {
            return keys.begin();
        }

        [[nodiscard]] std::vector<string_t>::const_iterator keys_end() const
        {
            return keys.end();
        }

        [[nodiscard]] std::vector<string_t>::const_iterator values_begin() const
        {
            return values.begin();
        }

        [[nodiscard]] std::vector<string_t>::const_iterator values_end() const
        {
            return values.end();
        }
    };

    class json_parser_t final
    {
        std::unordered_map<string_t, int> search_table;
        std::vector<std::shared_ptr<json_namespace_t>> json;
        size_t curr_ns = 0;

        enum class ns_state_e{NS_STATE_NONE = -1, NS_STATE_CLOSE = 0, NS_STATE_OPEN  = 1};

    private:
        string_t parse_key_or_value(const string_t& line, size_t& start, ns_state_e& ns_state)
        {
            string_t str{};
            bool is_reading = false;

            if (start > line.length() || ns_state != ns_state_e::NS_STATE_NONE)
                return str;

            for (size_t i = start; i < line.length(); i++, start = i)
            {
                char c = line[i];

                if (is_reading)
                {
                    if (c == '"' || c == '\\' || c == '\'' || c == ',' || c == ':')
                    {
                        try
                        {
                            // check for escape
                            if (str.at(str.length() - 1) !=  '\\')
                            {
                                // end of value/key/line
                                break;
                            }
                        }
                        catch(const std::out_of_range& e)
                        {
                            continue;
                        }
                    }
                    else if (c == '}')
                    {
                        // closing namespace
                        ns_state = ns_state_e::NS_STATE_CLOSE;
                        break;
                    }
                    else if (c == '{')
                    {
                        // start namespace
                        ns_state = ns_state_e::NS_STATE_OPEN;
                        break;
                    }

                    // no more whitespaces!
                    if( c != ' ')
                    {
                        str += c;
                    }
                }
                else if (c == '"' || c == '\\' || c == '\'' || c == ':')
                {
                    is_reading = true;
                    
                    // escape whitespaces
                    int j = i;
                    while (c == ' ' && j < line.length())
                    {
                        c = line[i];
                    }
                    i = j - 1;
                } 
                else if (c == '}')
                {
                    // closing namespace
                    ns_state = ns_state_e::NS_STATE_CLOSE;
                    break;
                }
                else if (c == '{')
                {
                    // start namespace
                    ns_state = ns_state_e::NS_STATE_OPEN;
                    break;
                }
            }
 
            ++start;
            return str;
        }

        void parse_namespace_open(const string_t& key, size_t& i)
        {
            if (!json.empty())
            {
                ++curr_ns;
            }

            auto new_namespace = std::make_shared<json_namespace_t>();
            json.push_back(new_namespace);

            if (curr_ns > 0)
            {
                string_t curr_namespace = json[curr_ns - 1]->Namespace();
                curr_namespace.append(".").append(key);

                curr_ns = json.size() - 1;
                new_namespace->SetNamespace(curr_namespace);
            }

            search_table.emplace(new_namespace->Namespace(), curr_ns);
        }

        void parse_namespace_close(const string_t& key, size_t& i)
        {
            if (curr_ns > 0)
            {
                --curr_ns;
            }
        }

        void parse_line(const string_t& line)
        {
            size_t i = 0;
            ns_state_e ns_state = ns_state_e::NS_STATE_NONE;
            string_t key = parse_key_or_value(line, i, ns_state);

            if (key.empty() && ns_state == ns_state_e::NS_STATE_NONE)
            {
                return;
            }

            if (ns_state == ns_state_e::NS_STATE_OPEN)
            {
                parse_namespace_open(key, i);
            }
            else if (ns_state == ns_state_e::NS_STATE_CLOSE)
            {
                parse_namespace_close(key, i);
            }
            else
            {
                string_t value = parse_key_or_value(line, i, ns_state);

                if (!value.empty())
                {
                    json[curr_ns]->Add(key, value);
                }
                else if (ns_state == ns_state_e::NS_STATE_OPEN)
                {
                    parse_namespace_open(key, i);
                }
                else if (ns_state == ns_state_e::NS_STATE_CLOSE)
                {
                    parse_namespace_close(key, i);
                }
            }

            // continue parsing in that line
            if (i < line.size())
            {
                string_t line_frag(line.begin() + i, line.end());
                parse_line(line_frag);
            }
        }

    public:
        json_parser_t() = default;

        void parse(const string_t& json_string)
        {
            string_t line{};
            for (char c : json_string)
            {
                line += c;

                if (c == '\n' || c == EOF)
                {
                    parse_line(line);
                    line.clear();
                }
            }

            if(!line.empty())
            {
                parse_line(line);
            }
        }

        void parse_file(const string_t& filename)
        {
            assert(!filename.empty());

            std::ifstream fd(filename);

            if (fd.is_open())
            {
                string_t line{};
                while (std::getline(fd, line))
                {
                    parse_line(line);
                }
                fd.close();
            }
        }

        void clear()
        {
            json.clear();
            search_table.clear();
        }

        std::shared_ptr<json_namespace_t> get(const string_t& name_of_namespace)
        {
            auto it = search_table.find(name_of_namespace);
            
            if (it != search_table.end())
            {
                return json.at(it->second);
            }

            return std::make_shared<json_namespace_t>();
        }

        std::shared_ptr<json_namespace_t> operator[](string_t name_of_namespace)
        {
            return get(name_of_namespace);
        }
    };
}

#endif // __JSON_PARSER_HPP__
