using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WalletWasabi.BuyAnything.Models;
using WalletWasabi.Logging;

public record BuyAnythingConfig(
	string SmtpServer,
	string RecipientEmail,
	string SenderPassword,
	string EmailDomain,
	string MessageInboxFolder);

[ApiController]
[Route("api/[controller]")]
public class BuyAnythingController : ControllerBase
{
	private readonly BuyAnythingConfig _buyAnythingConfig;

	// Inject BuyAnythingConfig dependency through constructor
	public BuyAnythingController(BuyAnythingConfig buyAnythingConfig)
	{
		_buyAnythingConfig = buyAnythingConfig;
	}

	[HttpPost("send")]
	public async Task<ActionResult> Send([FromBody] BuyAnythingMessage request)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(ModelState);
		}

		try
		{
			// Construct the sender's email address using the requestId and domain from config
			string senderEmail = $"{request.RequestId}@{_buyAnythingConfig.EmailDomain}";

			// Create a SmtpClient object and send the email asynchronously
			using SmtpClient smtpClient = new(_buyAnythingConfig.SmtpServer){
				UseDefaultCredentials = false,
				Credentials = new NetworkCredential(senderEmail, _buyAnythingConfig.SenderPassword),
				EnableSsl = true // Set to true if your SMTP server requires SSL
			};

			// Create a MailMessage object
			using MailMessage mailMessage = new (senderEmail, _buyAnythingConfig.RecipientEmail)
			{
				Subject = request.RequestId, // Set the subject as TBD or provide a specific subject
				Body = request.Message // Set the message body
			};

			await smtpClient.SendMailAsync(mailMessage);

			return Ok("Request sent successfully!");
		}
		catch (Exception ex)
		{
			Logger.LogError("Error sending email.", ex);
			return BadRequest($"Error sending the request.");
		}
	}

	[HttpPost("reply")]
	public async Task<ActionResult> Reply([FromBody] BuyAnythingMessage request)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(ModelState);
		}

		try
		{
			// Construct the file name using the requestId and current time
			string fileName = $"{request.RequestId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";

			// Convert the EmailRequest to JSON and save it to a text file
			string filePath = Path.Combine(_buyAnythingConfig.MessageInboxFolder, fileName); // Replace with the actual directory path
			string jsonContent = JsonConvert.SerializeObject(request);
			await System.IO.File.WriteAllTextAsync(filePath, jsonContent);

			return Ok($"Reply saved successfully");
		}
		catch (Exception ex)
		{
			Logger.LogError("Error saving reply.", ex);
			return BadRequest($"Error saving reply: {ex.Message}");
		}
	}

	[HttpGet("getreplies")]
	public async Task<ActionResult<IEnumerable<BuyAnythingMessage>>> GetRepliesAsync(string requestId, long since)
	{
		try
		{
			// Construct the search pattern for files in the inbox folder
			string searchPattern = $"{requestId}_*.txt";

			// Get all files matching the pattern in the inbox folder
			string[] filePaths = Directory.GetFiles(_buyAnythingConfig.MessageInboxFolder, searchPattern);

			DateTimeOffset sinceDateTime = DateTimeOffset.FromUnixTimeSeconds(since);

			// Filter files based on the since parameter
			List<BuyAnythingMessage> replies = new List<BuyAnythingMessage>();
			foreach (string filePath in filePaths)
			{
				// Extract the timestamp part of the file name and convert it to DateTime
				string timestampPart = Path.GetFileNameWithoutExtension(filePath).Substring(requestId.Length + 1);
				long fileTimestamp = long.Parse(timestampPart);
				DateTimeOffset fileDateTime = DateTimeOffset.FromUnixTimeSeconds(fileTimestamp);

				// Check if the file is newer than the 'since' parameter
				if (fileDateTime > sinceDateTime)
				{
					// Read the file content and deserialize it to BuyAnythingMessage
					string jsonContent = await System.IO.File.ReadAllTextAsync(filePath);
					BuyAnythingMessage reply = JsonConvert.DeserializeObject<BuyAnythingMessage>(jsonContent);
					replies.Add(reply);
				}
			}

			return Ok(replies);
		}
		catch (Exception ex)
		{
			return BadRequest($"Error retrieving replies: {ex.Message}");
		}
	}
}
