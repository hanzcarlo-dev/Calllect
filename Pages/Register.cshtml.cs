
using CallLect.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using System.Net.Mail;

namespace CallLect.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly DbConnection _db;

        public RegisterModel(DbConnection db)
        {
            _db = db;
        }

        [BindProperty]
        public string FullName { get; set; } = "";

        [BindProperty]
        public string Username { get; set; } = "";

        [BindProperty]
        public string Email { get; set; } = "";

        [BindProperty]
        public string Password { get; set; } = "";

        [BindProperty]
        public string ConfirmPassword { get; set; } = "";

        public string ErrorMessage { get; set; } = "";

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Username) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password) ||
                string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ErrorMessage = "Please complete all fields.";
                return Page();
            }

            if (Username.Length < 3 || Username.Length > 30)
            {
                ErrorMessage = "Username must be 3 to 30 characters.";
                return Page();
            }

            if (Username.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
            {
                ErrorMessage = "Username can only contain letters, numbers, and underscores.";
                return Page();
            }

            try
            {
                var address = new MailAddress(Email);

                if (address.Address != Email.Trim())
                {
                    ErrorMessage = "Enter a valid email address.";
                    return Page();
                }
            }
            catch
            {
                ErrorMessage = "Enter a valid email address.";
                return Page();
            }

            if (Password.Length < 8)
            {
                ErrorMessage = "Password must contain at least 8 characters.";
                return Page();
            }

            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return Page();
            }

            using var connection = _db.CreateConnection();
            await connection.OpenAsync();

            const string checkSql = @"
                SELECT COUNT(*)
                FROM calllect_users
                WHERE username = @username OR email = @email";

            using var checkCommand = new MySqlCommand(checkSql, connection);

            checkCommand.Parameters.AddWithValue("@username", Username.Trim());
            checkCommand.Parameters.AddWithValue("@email", Email.Trim());

            int existing = Convert.ToInt32(
                await checkCommand.ExecuteScalarAsync());

            if (existing > 0)
            {
                ErrorMessage = "Username or email is already registered.";
                return Page();
            }

            string passwordHash = PasswordHelper.HashPassword(Password);

            const string insertSql = @"
                INSERT INTO calllect_users
                (full_name, username, email, password_hash)
                VALUES
                (@name, @username, @email, @password)";

            using var insertCommand =
                new MySqlCommand(insertSql, connection);

            insertCommand.Parameters.AddWithValue("@name", FullName.Trim());
            insertCommand.Parameters.AddWithValue("@username", Username.Trim());
            insertCommand.Parameters.AddWithValue("@email", Email.Trim());
            insertCommand.Parameters.AddWithValue("@password", passwordHash);

            await insertCommand.ExecuteNonQueryAsync();

            TempData["RegistrationMessage"] =
                "Your account has been created. You can now log in.";

            return RedirectToPage("/Login");
        }
    }
}
