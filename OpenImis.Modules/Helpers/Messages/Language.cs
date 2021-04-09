﻿using System.Reflection;

namespace OpenImis.Modules.Helpers.Messages
{
    public class Language
    {
        public string GetMessage(int language, string name)
        {
            FieldInfo fieldInfos;

            switch (language)
            {

                case 0:
                    fieldInfos = typeof(PrimaryLanguage).GetField(name);
                    break;
                case 1:
                    fieldInfos = typeof(SecondaryLanguage).GetField(name);

                    break;
                default:
                    fieldInfos = typeof(PrimaryLanguage).GetField(name);

                    break;
            }
            var val = (string)fieldInfos.GetValue(null);
            return val;
        }
    }
}
