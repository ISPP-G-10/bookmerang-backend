namespace Bookmerang.Api.Services.Interfaces.PilotUsers

{
    public interface IWeeklyFeedbackMailService
    {
        /// <summary>
        /// Envía el correo de feedback a los usuarios piloto
        /// </summary>
        Task SendWeeklyFeedbackMailAsync(CancellationToken cancellationToken = default);
    }
}