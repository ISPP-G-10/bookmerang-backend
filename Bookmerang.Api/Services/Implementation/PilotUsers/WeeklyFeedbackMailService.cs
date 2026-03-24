using Bookmerang.Api.Services.Interfaces.PilotUsers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bookmerang.Api.Services.Implementation.PilotUsers
{
    public class WeeklyFeedbackMailService : BackgroundService, IWeeklyFeedbackMailService
    {
        private readonly ILogger<WeeklyFeedbackMailService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public WeeklyFeedbackMailService(
            ILogger<WeeklyFeedbackMailService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        // Este método es llamado automáticamente por el sistema en segundo plano
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Calcula el próximo jueves a las 10:00
                var now = DateTime.Now;
                var nextRun = now.Date.AddDays(((int)DayOfWeek.Wednesday - (int)now.DayOfWeek + 7) % 7).AddHours(11);

                // Si ya ha pasado la hora de hoy, programa para el jueves siguiente
                if (nextRun <= now)
                    nextRun = nextRun.AddDays(7);

                var delay = nextRun - now;
                await Task.Delay(delay, stoppingToken);

                try
                {
                    await SendWeeklyFeedbackMailAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error enviando el correo semanal de feedback.");
                }
            }
        }

        public async Task SendWeeklyFeedbackMailAsync(CancellationToken cancellationToken = default)
        {
            var emailsString = Environment.GetEnvironmentVariable("PILOT_USERS_EMAILS");
            var emails = emailsString?.Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (emails == null)
            {
                _logger.LogWarning("No se encontraron correos de usuarios piloto en la variable de entorno.");
                return;
            }

            var emailList = new System.Collections.Generic.List<string>();
            foreach (var email in emails)
                emailList.Add(email.Trim());

            using (var scope = _serviceProvider.CreateScope())
            {
                var pilotUsersService = scope.ServiceProvider.GetRequiredService<IPilotUsersService>();
                var result = await pilotUsersService.SendFeedbackMail(emailList);

                if (result)
                    _logger.LogInformation("Correo semanal de feedback enviado correctamente.");
                else
                    _logger.LogWarning("No se pudo enviar el correo semanal de feedback.");
            }
        }
    }
}