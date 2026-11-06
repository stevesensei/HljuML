namespace HljuML.Shared.Model;

public enum ServiceStatus
{
    Success = 0,
    //除了内部未处理错误之外的所有错误
    Failure = 1,
    InternalError = 2
}

public sealed class ServiceResult<TDataType>
{
    public ServiceStatus Status { get; set; }
    public string Message { get; set; } = null!;
    public TDataType? Data { get; set; }
    
    public static ServiceResult<TDataType> SuccessResult(TDataType? data = default, string message = "Success")
    {
        return new ServiceResult<TDataType>
        {
            Status = ServiceStatus.Success,
            Message = message,
            Data = data
        };
    }

    public static ServiceResult<TDataType> FailureResult(TDataType? data = default, string message = "Failure")
    {
        return new ServiceResult<TDataType>
        {
            Status = ServiceStatus.Failure,
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