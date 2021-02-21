using System;

namespace ModVersionChecker
{
    public static class Localization
    {
        public static JSONNode _translations;

        public static void LoadTranslations(string json)
        {
            _translations = JSON.Parse(json);
        }

        public static string Translate(this string s)
        {
            try
            {
                if (_translations.HasKey(s))
                {
                    var currentLocale = global::Localization.language.ToString();
                    if (_translations[s].HasKey(currentLocale))
                    {
                        if (_translations[s][currentLocale] != null)
                        {
                            return _translations[s][currentLocale];
                        }
                    }

                    if (_translations[s].HasKey("Other"))
                    {
                        if (_translations[s]["Other"] != null)
                        {
                            return _translations[s]["Other"];
                        }
                    }

                    if (global::Localization.language == Language.zhCN)
                    {
                        return s;
                    }

                    if (_translations[s].HasKey("enUS"))
                    {
                        if (_translations[s]["enUS"] != null)
                        {
                            return _translations[s]["enUS"];
                        }
                    }
                }
            }
            catch (Exception e)
            {
                return s;
            }

            // Fallback
            return s;
        }
    }
}