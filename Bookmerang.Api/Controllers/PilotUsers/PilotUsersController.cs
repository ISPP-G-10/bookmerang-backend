using Bookmerang.Api.Services.Interfaces.PilotUsers;
using Microsoft.AspNetCore.Mvc;

namespace Bookmerang.Api.Controllers.PilotUsers
{
    [ApiController]
    [Route("api/pilot-users")]
    public class PilotUsersController : ControllerBase
    {
        private readonly IPilotUsersService _pilotUsersService;

        public PilotUsersController(IPilotUsersService pilotUsersService)
        {
            _pilotUsersService = pilotUsersService;
        }

        /// <summary>
        /// Envía un correo de recordatorio a los usuarios piloto para que usen la app y envíen feedback.
        /// </summary>
        /// <param name="emails">Lista de correos de los usuarios piloto.</param>
        /// <returns>True si el envío fue exitoso, false en caso contrario.</returns>
        [HttpPost("send-feedback-mail")]
        public async Task<ActionResult<bool>> SendFeedbackMail([FromBody] List<string> emails)
        {
            var result = await _pilotUsersService.SendFeedbackMail(emails);
            return Ok(result);
        }
    }
}