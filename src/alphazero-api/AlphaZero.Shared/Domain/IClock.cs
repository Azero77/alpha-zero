namespace AlphaZero.Shared.Domain;

public interface IClock
{
    public DateTime Now => DateTime.UtcNow;
}
public class Clock : IClock
{

}