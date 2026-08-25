namespace BaseCore.DTO.Response
{
    //These message will be used to match the language used in angularjs translate.
    public static class ConstantResponseMessage
    {
        public static readonly string SUCCESS = "SUCCESS";
        public static readonly string RECORD_ADDED = "RECORD_ADDED";
        public static readonly string RECORD_ADDED_FAIL = "RECORD_ADDED_FAIL";
        public static readonly string RECORD_UPDATED = "RECORD_UPDATED";
        public static readonly string RECORD_UPDATED_FAIL = "RECORD_UPDATED_FAIL";
        public static readonly string RECORD_CANCELLED = "RECORD_CANCELLED";
        public static readonly string RECORD_REMOVED = "RECORD_REMOVED";
        public static readonly string RECORD_REMOVED_FAIL = "RECORD_REMOVED_FAIL";
        public static readonly string RECORD_COPY_TO = "RECORD_COPY_TO";
        public static readonly string RECORD_COPY_TO_FAIL = "RECORD_COPY_TO_FAIL";
        public static readonly string RECORD_APPROVED = "RECORD_APPROVED";
        public static readonly string RECORD_REJECTED = "RECORD_REJECTED";
        public static readonly string RECORD_NOT_ALLOW_REMOVE = "RECORD_NOT_ALLOW_REMOVE";
        public static readonly string MSG_ACTIVE_DEACTIVE_FAIL = "MSG_ACTIVE_DEACTIVE_FAIL";

        public static readonly string INTERNAL_SERVER_ERROR = "INTERNAL_SERVER_ERROR";
        public static readonly string DUPLICATED_RECORD = "DUPLICATED_RECORD";
        public static readonly string INVALID_INPUT = "INVALID_INPUT";
        public static readonly string RECORD_NOT_FOUND = "MSG_RECORD_NOT_FOUND";
        public static readonly string RECORD_ALREADY_EXISTS = "RECORD_ALREADY_EXISTS";
        public static readonly string NOT_PERMISSION = "NOT_PERMISSION";
        public static readonly string EXIST_ACTION_CODE = "Mã điều khiển không được trùng! ";


        public static readonly string MSG_INVALID_INPUT = "There are some field not input correctly!";
        public static readonly string MSG_RECORD_ADDED = "Record added successfully!";
        public static readonly string MSG_RECORD_ADDED_FAIL = "Record added fail!";
        public static readonly string MSG_RECORD_UPDATED = "Record updated successfully!";
        public static readonly string MSG_RECORD_UPDATED_FAIL = "Record updated fail!";
        public static readonly string MSG_DUPLICATED_RECORD = "Ops, Record exists!";
        public static readonly string MSG_RECORD_NOT_ALLOW_REMOVE = "Record unable to delete, please try another record.";
        public static readonly string MSG_RECORD_REMOVED = "Record removed successfully.";
        public static readonly string MSG_RECORD_REMOVED_FAIL = "There is a error when delete, Please try again or confirm with Admin.";

        #region Group 

        public static readonly string GROUP_VALID_PARAMS = "GROUP_VALID_PARAMS";

        #endregion
    }

    //TODO
    public static class ConstantResponseCode
    {
        public const int SUCCESS = 0;
        public const int ERROR = 1;
        public const int WARNING = 2;
        public const int INFO = 3;
    }
}
