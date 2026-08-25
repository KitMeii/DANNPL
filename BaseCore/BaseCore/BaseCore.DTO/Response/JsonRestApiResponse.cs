namespace BaseCore.DTO.Response
{
    public class JsonRestApiResponse
    {
        public bool IsRequestSuccess { get; set; }
        public object Payload { get; set; }
        public ErrorResponseMessage Error { get; set; }
        public JsonRestApiResponse(bool isRequestSuccess, object payload = null)
        {
            IsRequestSuccess = isRequestSuccess;
            Payload = payload;
            Error = null;
        }

        public JsonRestApiResponse(bool isRequestSuccess, string errorCode, string errorMessage = "", object payload = null)
        {
            IsRequestSuccess = isRequestSuccess;
            Payload = payload;
            Error = new ErrorResponseMessage(errorCode, errorMessage);
        }
    }

    public class ErrorResponseMessage
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public ErrorResponseMessage(string code, string message)
        {
            Code = code;
            Message = message;
        }
    }

    public interface IRestApiResponse
    {
        ResponseMessage Create(bool isSuccess, int statusCode, string statusMessage, object payload);
    }

    public class ResponseCreator : IRestApiResponse
    {
        public ResponseMessage Create(bool isSuccess, int statusCode, string statusMessage, object payload = null)
        {
            ResponseMessage message = new ResponseMessage
            {
                IsSuccess = statusCode == ConstantResponseCode.SUCCESS,
                StatusCode = statusCode,
                Message = statusMessage,
                Payload = payload
            };
            return message;
        }
    }

    public class ResponseFactory
    {
        public static ResponseMessage GetResponse(int statusCode, string message, object payload = null)
        {
            var objResponse = new ResponseCreator();
            return objResponse.Create(false, statusCode, message, payload);
        }

        public static ResponseMessage GetResponseOk(object payload = null)
        {
            var objResponse = new ResponseCreator();
            return objResponse.Create(true, ConstantResponseCode.SUCCESS, "", payload);
        }
    }

    public class ResponseMessage
    {
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; }
        public object Payload { get; set; }
    }

}
