using System;

namespace BaseCore.Common
{
    public static class Constants
    {
        public static int PAGE_SIZE_DEFAULT = 10;
        public static string FORMAT_DATE_TIME = "dd/MM/yyyy hh:MM:ss"; 
        public static string FORMAT_DATE = "dd/MM/yyyy";


        public static string RootCache = "PLM_";

        public static string Table_User = "user";
        public static string Table_Role = "role";
        public static string Table_Module = "module";
        public static string Table_Function = "function";
        public static string Table_Doctor = "doctor";
        public static string Table_Area = "area";
        public static string Table_Robot = "robot";
        public static string Table_Camera = "camera";
        public static string Table_RobotVersion = "robotversion";
        public static string Table_Setting = "robotversion";

        public static string KeyGetListUser = Table_User + ":GetListUser_{0}_{1}:{2}";
        public static string KeyGetListRole = Table_Role + ":GetListRole_{0}_{1}:{2}";
        public static string KeyGetListModule = Table_Module + ":GetListModule_{0}_{1}:{2}";
        public static string KeyGetListFunction = Table_Function + ":GetListFunction_{0}_{1}:{2}";
        public static string KeyGetListDoctor = Table_Doctor + ":GetListDoctor_{0}_{1}:{2}";
        public static string KeyGetListArea = Table_Area + ":GetListArea_{0}_{1}:{2}";
        public static string KeyGetListRobot = Table_Robot + ":GetListRobot_{0}_{1}:{2}";
        public static string KeyGetListCamera = Table_Camera + ":GetListCamera_{0}_{1}:{2}";
        public static string KeyGetListRobotVersion = Table_RobotVersion + ":GetListRobotVersion_{0}_{1}:{2}";
        public static string KeyGetListSetting = Table_Setting + ":GetListSetting_{0}_{1}:{2}";
    }
}

