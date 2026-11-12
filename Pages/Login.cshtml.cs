
using CallLect.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace CallLect.Pages
{
    public class LoginModel : PageModel
    {
        private readonly DbConnection _db;

        public LoginModel(DbConnection db)
        {
            _db = db;
        }

        [BindProperty]
        public string Username { get; set; } = "";

        [BindProperty]
        public string Password { get; set; } = "";

        public string ErrorMessage { get; set; } = "";

        public void OnGet()
        {
            if (HttpContext.Session.GetString("LoggedIn") == "true")
                Response.Redirect("/");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) ||
                string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter your username and password.";
                return Page();
            }

            using var connection = _db.CreateConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT id, username, password_hash
                FROM calllect_users
                WHERE username = @username OR email = @username
                LIMIT 1";

            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@username", Username.Trim());

            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                ErrorMessage = "Invalid username or password.";
                return Page();
            }

            int userId = reader.GetInt32("id");
            string storedUsername = reader.GetString("username");
            string storedPassword = reader.GetString("password_hash");

            if (!PasswordHelper.VerifyPassword(Password, storedPassword))
            {
                ErrorMessage = "Invalid username or password.";
                return Page();
            }

            HttpContext.Session.SetString("LoggedIn", "true");
            HttpContext.Session.SetString("UserId", userId.ToString());
            HttpContext.Session.SetString("Username", storedUsername);

            return RedirectToPage("/Index");
        }
    }
}
