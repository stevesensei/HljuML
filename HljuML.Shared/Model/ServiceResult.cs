namespace HljuML.Shared.Model;

public enum ServiceStatus
{
    Success = 0,
    //除了内部未处理错误之外的所有错误
    Failure = 1,
    InternalError = 2
}

/// <summary>供调用方决定恢复流程的错误类型，不依赖界面文案判断。</summary>
public enum ServiceErrorCode
{
    None = 0,
    AuthenticationRequired = 1
}

public sealed class ServiceResult<TDataType>
{
    public ServiceStatus Status { get; set; }
    public string Message { get; set; } = null!;
    public TDataType? Data { get; set; }
    public ServiceErrorCode ErrorCode { get; set; }
    
    public static ServiceResult<TDataType> SuccessResult(TDataType? data = default, string message = "Success")
    {
        return new ServiceResult<TDataType>
        {
            Status = ServiceStatus.Success,
            Message = message,
            Data = data
        };
    }

    public static ServiceResult<TDataType> FailureResult(TDataType? data = default, string message = "Failure",
        ServiceErrorCode errorCode = ServiceErrorCode.None)
    {
        return new ServiceResult<TDataType>
        {
            Status = ServiceStatus.Failure,
            ErrorCode = errorCode,
            Message = message,
            Data = data
        };
    }

    public static ServiceResult<TDataType> InternalErrorResult(TDataType? data = default, string message = "Internal Error")
    {
        return new ServiceResult<TDataType>
        {
            Status = ServiceStatus.InternalError,
            Message = message,
            Data = data
        };
    }

}
