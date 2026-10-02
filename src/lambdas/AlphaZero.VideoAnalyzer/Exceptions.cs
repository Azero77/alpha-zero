namespace AlphaZero.VideoPipeline.Exceptions;

public class TransientException : Exception
{
    public TransientException(string message) : base(message) { }
    public TransientException(string message, Exception inner) : base(message, inner) { }
}

public class VideoProcessingException : Exception
{
    public VideoProcessingException(string message) : base(message) { }
    public VideoProcessingException(string message, Exception inner) : base(message, inner) { }
}
