using Microsoft.AspNetCore.Mvc;
using WhatsAppDemo.Api.Models;
using WhatsAppDemo.Api.Services;

namespace WhatsAppDemo.Api.Controllers
{
    [ApiController]
    [Route("api/whatsapp")]
    public class WhatsAppController : ControllerBase
    {
        private readonly IWhatsAppService _whatsAppService;
        
        public WhatsAppController(IWhatsAppService whatsAppService)
        {
            _whatsAppService = whatsAppService;
        }
        
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage(
        [FromBody] SendWhatsAppMessageRequest request)
        {
            var result = await _whatsAppService.SendMessageAsync(
                request.PhoneNumber,
                request.Message);

            if (!result)
            {
                return BadRequest("Message could not be sent.");
            }

            return Ok(new
            {
                success = true,
                message = "Message sent successfully."
            });
        }

        ///// <summary>
        ///// Receives incoming WhatsApp messages from the WhatsApp API provider.
        ///// </summary>
        //[HttpPost("webhook")]
        //public IActionResult ReceiveWebhook(
        //    [FromBody] WhatsAppWebhookRequest request)
        //{
        //    Console.WriteLine("========== WhatsApp Webhook ==========");

        //    Console.WriteLine($"Message ID : {request.MessageId}");
        //    Console.WriteLine($"From       : {request.From}");
        //    Console.WriteLine($"Type       : {request.Type}");
        //    Console.WriteLine($"Message    : {request.Message}");
        //    Console.WriteLine($"Timestamp  : {request.Timestamp}");
        //    Console.WriteLine($"Media URL  : {request.MediaUrl}");
        //    Console.WriteLine($"File Name  : {request.FileName}");

        //    Console.WriteLine("======================================");

        //    return Ok(new
        //    {
        //        success = true,
        //        message = "WhatsApp message received successfully."
        //    });
        //}


        ///// <summary>
        ///// Sends a WhatsApp message through the configured WhatsApp API provider.
        ///// </summary>
        //[HttpPost("send")]
        //public IActionResult SendMessage(
        //    [FromBody] SendWhatsAppMessageRequest request)
        //{
        //    if (string.IsNullOrWhiteSpace(request.To))
        //    {
        //        return BadRequest(new
        //        {
        //            success = false,
        //            message = "Recipient phone number is required."
        //        });
        //    }

        //    if (string.IsNullOrWhiteSpace(request.Message))
        //    {
        //        return BadRequest(new
        //        {
        //            success = false,
        //            message = "Message is required."
        //        });
        //    }

        //    // Actual WhatsApp provider API call
        //    // will be implemented after selecting the provider.

        //    Console.WriteLine("========== Send WhatsApp ==========");
        //    Console.WriteLine($"To      : {request.To}");
        //    Console.WriteLine($"Message : {request.Message}");
        //    Console.WriteLine("===================================");

        //    return Ok(new
        //    {
        //        success = true,
        //        message = "Message request received.",
        //        data = new
        //        {
        //            to = request.To,
        //            message = request.Message
        //        }
        //    });
        //}

        //[HttpPost("webhook")]
        //public IActionResult ReceiveWebhook([FromBody] WhatsAppWebhookRequest request)
        //{
        //    Console.WriteLine($"From: {request.From}");
        //    Console.WriteLine($"Message: {request.Message}");
        //    Console.WriteLine($"Type: {request.Type}");
        //    Console.WriteLine($"Message ID: {request.MessageId}");

        //    return Ok();
        //}

        //[HttpPost("send")]
        //public async Task<IActionResult> SendMessage(
        //[FromBody] SendWhatsAppMessageRequest request)
        //{
        //    // Call WhatsApp provider API here

        //    return Ok();
        //}
    }
}
