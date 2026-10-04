namespace PtclSharp;

public enum Version
{
    BotW = 440,
    TotK = 1531
}

public class VersionException : Exception
{
    public VersionException(string message) : base(message)
    {
    }
}