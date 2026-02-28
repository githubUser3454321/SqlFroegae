namespace SqlFroega.Application.Abstractions;

public interface IHostIdentityProvider
{
    string GetWindowsUserName();
    string GetComputerName();
}
