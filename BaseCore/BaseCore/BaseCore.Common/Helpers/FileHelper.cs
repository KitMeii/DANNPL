using System;
using System.Collections.Generic;
using System.Text;

namespace BaseCore.Common.Helpers
{
    public class FileHelper
    {
        public static byte[] GetFile(string s, out string errorMessage)
        {
            errorMessage = "";
            try
            {
                using (System.IO.FileStream fs = System.IO.File.OpenRead(s))
                {
                    byte[] data = new byte[fs.Length];
                    int br = fs.Read(data, 0, data.Length);
                    if (br != fs.Length)
                        throw new System.IO.IOException(s);
                    return data;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"error:{ex.Message},stacktrace:{ex.StackTrace}";

                return null;
            }
          
        }
    }
}
