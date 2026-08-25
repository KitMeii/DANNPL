using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace BaseCore.Common.Helpers
{
    public static class EnumHelper<T>
    {
        public static IList<T> GetValues(Enum value)
        {
            var enumValues = new List<T>();

            foreach (FieldInfo fi in value.GetType().GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                enumValues.Add((T)Enum.Parse(value.GetType(), fi.Name, false));
            }
            return enumValues;
        }

        public static T Parse(string value)
        {
            return (T)Enum.Parse(typeof(T), value, true);
        }

        public static T Parse(int value)
        {
            return (T)Enum.Parse(typeof(T), value.ToString(), true);
        }

        public static T Parse(int? value)
        {
            if (!value.HasValue) return default(T);
            return (T)Enum.Parse(typeof(T), value.ToString(), true);
        }

        public static IList<string> GetNames(Enum value)
        {
            return value.GetType().GetFields(BindingFlags.Static | BindingFlags.Public).Select(fi => fi.Name).ToList();
        }

        public static IList<string> GetDisplayValues(Enum value)
        {
            return GetNames(value).Select(obj => GetDisplayValue(Parse(obj))).ToList();
        }

        private static string LookupResource(Type resourceManagerProvider, string resourceKey)
        {
            foreach (PropertyInfo staticProperty in resourceManagerProvider.GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (staticProperty.PropertyType == typeof(System.Resources.ResourceManager))
                {
                    System.Resources.ResourceManager resourceManager = (System.Resources.ResourceManager)staticProperty.GetValue(null, null);
                    return resourceManager.GetString(resourceKey);
                }
            }

            return resourceKey; // Fallback with the key name
        }

        public static string GetDisplayValue(T value)
        {
            try
            {
                if (value == null) return "";
                var fieldInfo = value.GetType().GetField(value.ToString());
                if (fieldInfo == null) return "";
                var descriptionAttributes = fieldInfo.GetCustomAttributes(
                    typeof(DisplayAttribute), false) as DisplayAttribute[];

                if (descriptionAttributes[0].ResourceType != null)
                    return LookupResource(descriptionAttributes[0].ResourceType, descriptionAttributes[0].Name);

                if (descriptionAttributes == null) return string.Empty;
                return (descriptionAttributes.Length > 0) ? descriptionAttributes[0].Name : value.ToString();
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }

        public static string GetDisplayValue(int value)
        {
            var en = Parse(value);
            return GetDisplayValue(en);
        }

        public static List<KeyValuePair<int, string>> GetList()
        {
            return Enum.GetValues(typeof(T)).Cast<T>().Select(x => new KeyValuePair<int, string>((int)Enum.ToObject(typeof(T), x), EnumHelper<T>.GetDisplayValue(x))).ToList();
        }

        public static List<KeyValuePair<string, string>> GetStringList()
        {
            return Enum.GetValues(typeof(T)).Cast<T>().Select(x => new KeyValuePair<string, string>(((int)Enum.ToObject(typeof(T), x)) + "", EnumHelper<T>.GetDisplayValue(x))).ToList();
        }
    }
}
