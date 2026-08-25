using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static BaseCore.Common.Enums;

namespace BaseCore.Common.Extensions
{
    public static class MediaExtension
    {
        private static List<string> videoExtensions = new List<string>() { "mp4", "m4a", "m4v", "f4v", "f4a", "m4b", "m4r", "f4b", "mov","avi", "flv", "wmv"
                , "wma", "asf","ogg", "oga", "ogv", "ogx","webm","3gp", "3gp2", "3g2", "3gpp", "3gpp2"};

        private static List<string> imageExtensions = new List<string> { "png", "jpg", "jpeg", "bmp", "gif", "svg" };

        private static List<string> pdfExtensions = new List<string>() { "pdf" };
        private static List<string> docExtensions = new List<string>() { "doc", "docx" };
        public static MediaType GetMediaType(this string fileExtension)
        {
            if (string.IsNullOrEmpty(fileExtension))
                return MediaType.Unkown;

            var extensionToLower = fileExtension.ToLower();
            if (imageExtensions.Any(x => fileExtension.Contains(x)))
                return MediaType.Image;

     
            if (pdfExtensions.Any(x => fileExtension.Contains(x)))
                return MediaType.Pdf;


            if (docExtensions.Any(x => fileExtension.Contains(x)))
                return MediaType.Doc;

            if (videoExtensions.Any(x => fileExtension.Contains(x)))
                return MediaType.Video;
            return MediaType.File;
        }


    }
}
