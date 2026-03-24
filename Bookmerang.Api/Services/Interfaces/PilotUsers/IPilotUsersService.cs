namespace Bookmerang.Api.Services.Interfaces.PilotUsers;

public interface IPilotUsersService
{
    Task<bool> SendFeedbackMail(List<String> toEmails);
}