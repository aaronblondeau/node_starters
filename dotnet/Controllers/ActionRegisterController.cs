using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace HasuraStarter.Controllers;

[ApiController]
[Route("hasura/actions/register")]
public class ActionRegisterController : ControllerBase
{
    private readonly ILogger<ActionRegisterController> _logger;

    public ActionRegisterController(ILogger<ActionRegisterController> logger)
    {
        _logger = logger;
    }

    [HttpPost(Name = "ActionRegister")]
    public async Task<IActionResult> Post([FromBody] RegisterRequest registerRequest)
    {
        string jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? string.Empty;
        if (jwtSecret == string.Empty) {
            return StatusCode(StatusCodes.Status400BadRequest, new { message = "Authentication action is not properly configured!" });
        }

        if (registerRequest.input == null || registerRequest.input.email == null) {
            return StatusCode(StatusCodes.Status400BadRequest, new { message = "Email is required." });
        }
        if (!AuthCrypt.IsValidEmail(registerRequest.input.email))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new { message = "Email is invalid." });
        }

        if (registerRequest.input == null || registerRequest.input.password == null)
        {
            return StatusCode(StatusCodes.Status400BadRequest, new { message = "Password is required." });
        }
        if (registerRequest.input.password.Length < 5)
        {
            return StatusCode(StatusCodes.Status400BadRequest, new { message = "Password must be at least 5 characters long." });
        }

        string email = registerRequest.input.email.ToLower();
        string password = registerRequest.input.password;
        string hashedPassword = AuthCrypt.HashPassword(password);

        HttpClient hasuraClient = new HttpClient();
        hasuraClient.DefaultRequestHeaders.Add("x-hasura-admin-secret", Environment.GetEnvironmentVariable("HASURA_GRAPHQL_ADMIN_SECRET"));

        var registerResponse = await hasuraClient.PostAsync((Environment.GetEnvironmentVariable("HASURA_BASE_URL") ?? "http://localhost:8000") + "/v1/graphql", new StringContent(JsonSerializer.Serialize(new
        {
            operationName = "CreateUser",
            query = $@"mutation CreateUser {{
              insert_users(objects: {{email: ""{email}"", password: ""{hashedPassword}""}}) {{
                returning {{
                    id
                    password
                }}
              }}
            }}",
            // variables = null
        }), System.Text.Encoding.UTF8, "application/json"));
        var registerResponseBody = await registerResponse.Content.ReadAsStringAsync();
        var register = JsonDocument.Parse(registerResponseBody);

        if (register.RootElement.TryGetProperty("errors", out JsonElement errors))
        {
            string errorMessage = errors[0].GetProperty("message").GetString() ?? "Unknown error";
            return StatusCode(StatusCodes.Status400BadRequest, new { message = errorMessage });
        }
        
        int id = register.RootElement.GetProperty("data").GetProperty("insert_users").GetProperty("returning")[0].GetProperty("id").GetInt32();

        
        string token = AuthCrypt.GenerateToken(id, jwtSecret);
        return Ok(new LoginRegisterResponse(id, token));
    }
}
