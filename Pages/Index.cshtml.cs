
using CallLect.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using System.Text;

namespace CallLect.Pages
{
    public class IndexModel : PageModel
    {
        private readonly DbConnection _db;

        public IndexModel(DbConnection db)
        {
            _db = db;
        }

        public List<Contact> Contacts { get; set; } = new();
        public List<Category> Categories { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string ViewMode { get; set; } = "all";

        [BindProperty(SupportsGet = true)]
        public string Search { get; set; } = "";

        [BindProperty(SupportsGet = true)]
        public int CategoryId { get; set; }

        [BindProperty]
        public Contact Input { get; set; } = new();

        [BindProperty]
        public int[] SelectedCategoryIds { get; set; } = Array.Empty<int>();

        [BindProperty]
        public string GroupName { get; set; } = "";

        [BindProperty]
        public string GroupColor { get; set; } = "#7048bd";

        [BindProperty]
        public int GroupId { get; set; }

        [BindProperty]
        public IFormFile? CsvFile { get; set; }

        [BindProperty]
        public string CountryCode { get; set; } = "+63";

        private static readonly string[] AllowedCountryCodes =
        {
            "+1", "+44", "+61", "+63", "+65",
            "+81", "+82", "+86", "+91", "+971"
        };

        public async Task OnGetAsync()
        {
            using var connection = _db.CreateConnection();
            await connection.OpenAsync();

            await LoadCategories(connection);
            await LoadContacts(connection);
        }

        // LIVE SEARCH FROM THE DATABASE
        public async Task<IActionResult> OnGetSearchContactsAsync(string term)
        {
            term = (term ?? "").Trim();

            if (term.Length == 0)
                return new JsonResult(Array.Empty<object>());

            if (term.Length > 80)
                term = term.Substring(0, 80);

            using var connection = _db.CreateConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT id, full_name
                FROM contacts
                WHERE is_deleted = FALSE
                  AND LOCATE(LOWER(@term), LOWER(full_name)) = 1
                ORDER BY full_name ASC
                LIMIT 10";

            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@term", term);

            var results = new List<object>();

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                results.Add(new
                {
                    id = reader.GetInt32("id"),
                    fullName = reader.GetString("full_name")
                });
            }

            return new JsonResult(results);
        }

        private string GetValidCountryCode()
        {
            return AllowedCountryCodes.Contains(CountryCode)
                ? CountryCode
                : "+63";
        }

        private string FormatPhoneNumber(string localNumber)
        {
            string digits = new string(
                (localNumber ?? "").Where(char.IsDigit).ToArray());

            string code = GetValidCountryCode();

            if (code == "+63" && digits.StartsWith("0"))
                digits = digits.Substring(1);

            return code + digits;
        }

        private static (string CountryCode, string LocalPhoneNumber)
            GetPhoneParts(string phoneNumber)
        {
            string phone = (phoneNumber ?? "").Trim();

            string[] codes = AllowedCountryCodes
                .OrderByDescending(x => x.Length)
                .ToArray();

            foreach (string code in codes)
            {
                if (phone.StartsWith(code))
                {
                    string local = phone.Substring(code.Length);
                    local = new string(local.Where(char.IsDigit).ToArray());

                    if (code == "+63" && local.StartsWith("0"))
                        local = local.Substring(1);

                    return (code, local);
                }
            }

            string digits = new string(
                phone.Where(char.IsDigit).ToArray());

            if (digits.StartsWith("0"))
                digits = digits.Substring(1);

            return ("+63", digits);
        }

        private async Task LoadCategories(MySqlConnection connection)
        {
            Categories.Clear();

            const string sql = @"
                SELECT id, name, color, sort_order, is_default
                FROM contact_categories
                ORDER BY sort_order, name";

            using var command = new MySqlCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                Categories.Add(new Category
                {
                    Id = reader.GetInt32("id"),
                    Name = reader.GetString("name"),
                    Color = reader.GetString("color"),
                    SortOrder = reader.GetInt32("sort_order"),
                    IsDefault = reader.GetBoolean("is_default")
                });
            }
        }

        private async Task LoadContacts(MySqlConnection connection)
        {
            Contacts.Clear();

            const string sql = @"
                SELECT
                    c.id,
                    c.full_name,
                    c.phone_number,
                    c.additional_phones,
                    c.email,
                    c.notes,
                    c.is_favorite,
                    GROUP_CONCAT(
                        DISTINCT cc.name
                        ORDER BY cc.name SEPARATOR ', '
                    ) AS group_names
                FROM contacts c
                LEFT JOIN contact_assignments ca
                    ON c.id = ca.contact_id
                LEFT JOIN contact_categories cc
                    ON ca.category_id = cc.id
                WHERE
                    (
                        (@deleted = 1 AND c.is_deleted = TRUE)
                        OR
                        (@deleted = 0 AND c.is_deleted = FALSE)
                    )
                    AND
                    (@favorites = 0 OR c.is_favorite = TRUE)
                    AND
                    (@categoryId = 0 OR EXISTS (
                        SELECT 1
                        FROM contact_assignments filter_ca
                        WHERE filter_ca.contact_id = c.id
                        AND filter_ca.category_id = @categoryId
                    ))
                    AND
                    (
                        c.full_name LIKE @search
                        OR c.phone_number LIKE @search
                        OR c.email LIKE @search
                    )
                GROUP BY c.id
                ORDER BY c.full_name ASC";

            using var command = new MySqlCommand(sql, connection);

            command.Parameters.AddWithValue(
                "@deleted", ViewMode == "deleted" ? 1 : 0);

            command.Parameters.AddWithValue(
                "@favorites", ViewMode == "favorites" ? 1 : 0);

            command.Parameters.AddWithValue("@categoryId", CategoryId);
            command.Parameters.AddWithValue(
                "@search", "%" + (Search ?? "") + "%");

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string savedPhone = reader.GetString("phone_number");
                var phoneParts = GetPhoneParts(savedPhone);

                Contacts.Add(new Contact
                {
                    Id = reader.GetInt32("id"),
                    FullName = reader.GetString("full_name"),
                    PhoneNumber = savedPhone,
                    CountryCode = phoneParts.CountryCode,
                    LocalPhoneNumber = phoneParts.LocalPhoneNumber,
                    AdditionalPhones = reader.IsDBNull(
                        reader.GetOrdinal("additional_phones"))
                        ? ""
                        : reader.GetString("additional_phones"),
                    Email = reader.IsDBNull(reader.GetOrdinal("email"))
                        ? ""
                        : reader.GetString("email"),
                    Notes = reader.IsDBNull(reader.GetOrdinal("notes"))
                        ? ""
                        : reader.GetString("notes"),
                    IsFavorite = reader.GetBoolean("is_favorite"),
                    GroupNames = reader.IsDBNull(
                        reader.GetOrdinal("group_names"))
                        ? ""
                        : reader.GetString("group_names")
                });
            }
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            if (string.IsNullOrWhiteSpace(Input.FullName) ||
                string.IsNullOrWhiteSpace(Input.PhoneNumber))
            {
                return RedirectToPage();
            }

            string formattedPhone = FormatPhoneNumber(Input.PhoneNumber);

            using var connection = _db.CreateConnection();
            await connection.OpenAsync();

            const string duplicateSql = @"
                SELECT COUNT(*) FROM contacts
                WHERE phone_number = @phone
                AND is_deleted = FALSE";

            using var duplicateCommand =
                new MySqlCommand(duplicateSql, connection);

            duplicateCommand.Parameters.AddWithValue(
                "@phone", formattedPhone);

            var duplicateCount = Convert.ToInt32(
                await duplicateCommand.ExecuteScalarAsync());

            if (duplicateCount > 0)
            {
                TempData["Message"] =
                    "That phone number already exists.";
                return RedirectToPage();
            }

            const string sql = @"
                INSERT INTO contacts
                (full_name, phone_number, additional_phones, email, notes)
                VALUES
                (@name, @phone, @additional, @email, @notes)";

            using var command = new MySqlCommand(sql, connection);

            command.Parameters.AddWithValue(
                "@name", Input.FullName.Trim());

            command.Parameters.AddWithValue(
                "@phone", formattedPhone);

            command.Parameters.AddWithValue(
                "@additional",
                Input.AdditionalPhones ?? "");

            command.Parameters.AddWithValue(
                "@email",
                Input.Email ?? "");

            command.Parameters.AddWithValue(
                "@notes", Input.Notes ?? "");

            await command.ExecuteNonQueryAsync();

            long newId = command.LastInsertedId;

            await SaveAssignments(connection, newId, SelectedCategoryIds);

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditAsync()
        {
            if (Input.Id <= 0 ||
                string.IsNullOrWhiteSpace(Input.FullName) ||
                string.IsNullOrWhiteSpace(Input.PhoneNumber))
            {
                return RedirectToPage();
            }

            string formattedPhone = FormatPhoneNumber(Input.PhoneNumber);

            using var connection = _db.CreateConnection();
            await connection.OpenAsync();

            const string duplicateSql = @"
                SELECT COUNT(*) FROM contacts
                WHERE phone_number = @phone
                AND id <> @id
                AND is_deleted = FALSE";

            using var duplicateCommand =
                new MySqlCommand(duplicateSql, connection);

            duplicateCommand.Parameters.AddWithValue(
                "@phone", formattedPhone);

            duplicateCommand.Parameters.AddWithValue("@id", Input.Id);

            if (Convert.ToInt32(
                await duplicateCommand.ExecuteScalarAsync()) > 0)
            {
                TempData["Message"] =
                    "That phone number is already used by another contact.";

                return RedirectToPage();
            }

            const string sql = @"
                UPDATE contacts
                SET full_name = @name,
                    phone_number = @phone,
                    additional_phones = @additional,
                    email = @email,
                    notes = @notes
                WHERE id = @id AND is_deleted = FALSE";

            using var command = new MySqlCommand(sql, connection);

            command.Parameters.AddWithValue("@name", Input.FullName.Trim());
            command.Parameters.AddWithValue("@phone", formattedPhone);
            command.Parameters.AddWithValue(
                "@additional", Input.AdditionalPhones ?? "");
            command.Parameters.AddWithValue("@email", Input.Email ?? "");
            command.Parameters.AddWithValue("@notes", Input.Notes ?? "");
            command.Parameters.AddWithValue("@id", Input.Id);

            await command.ExecuteNonQueryAsync();

            await using (var deleteAssignments = new MySqlCommand(
                "DELETE FROM contact_assignments WHERE contact_id = @id",
                connection))
            {
                deleteAssignments.Parameters.AddWithValue("@id", Input.Id);
                await deleteAssignments.ExecuteNonQueryAsync();
            }

            await SaveAssignments(connection, Input.Id, SelectedCategoryIds);

            return RedirectToPage();
        }

        private async Task SaveAssignments(
            MySqlConnection connection,
            long contactId,
            int[] categoryIds)
        {
            foreach (int categoryId in categoryIds.Distinct())
            {
                const string sql = @"
                    INSERT IGNORE INTO contact_assignments
                    (contact_id, category_id)
                    VALUES (@contact, @category)";

                using var command = new MySqlCommand(sql, connection);

                command.Parameters.AddWithValue("@contact", contactId);
                command.Parameters.AddWithValue("@category", categoryId);

                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            using var connection = _db.CreateConnection();
            await connection.OpenAsync();

            const string sql = @"
                UPDATE contacts
                SET is_deleted = TRUE,
                    deleted_at = NOW()
                WHERE id = @id AND is_deleted = FALSE";

            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            await command.ExecuteNonQueryAsync();

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRestoreAsync(int id)
        {
            using var connection = _db.CreateConnection();
            await connection.OpenAsync();

            const string sql = @"
                UPDATE contacts
                SET is_deleted = FALSE,
                    deleted_at = NULL
                WHERE id = @id AND is_deleted = TRUE";

            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            await command.ExecuteNonQueryAsync();

            return RedirectToPage(new { ViewMode = "deleted" });
        }

        public async Task<IActionResult> OnPostPermanentDeleteAsync(int id)
        {
            using var connection = _db.CreateConnection();
            await connection.OpenAsync();

            const string sql = @"
                DELETE FROM contacts
                WHERE id = @id AND is_deleted = TRUE";

            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            await command.ExecuteNonQueryAsync();

            return RedirectToPage(new { ViewMode = "deleted" });
        }

        public async Task<IActionResult> OnPostFavoriteAsync(int id)
        {
            using var connection = _db.CreateConnection();
            await connection.OpenAsync();

            const string sql = @"
                UPDATE contacts
                SET is_favorite = NOT is_favorite
                WHERE id = @id AND is_deleted = FALSE";

            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            await command.ExecuteNonQueryAsync();

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddGroupAsync()
        {
            if (string.IsNullOrWhiteSpace(GroupName))
                return RedirectToPage();

            using var connection = _db.CreateConnection();
            await connection.OpenAsync();

            const string sql = @"
                INSERT INTO contact_categories
                (name, color, sort_order, is_default)
                VALUES (@name, @color,
                    (SELECT COALESCE(MAX(sort_order), 0) + 1
                     FROM (SELECT sort_order
                           FROM contact_categories) AS existing),
                    FALSE)";

            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@name", GroupName.Trim());
            command.Parameters.AddWithValue("@color", GroupColor);

            await command.ExecuteNonQueryAsync();

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditGroupAsync()
        {
            if (GroupId <= 0 || string.IsNullOrWhiteSpace(GroupName))
                return RedirectToPage();

            using var connection = _db.CreateConnection();
            await connection.OpenAsync();

            const string sql = @"
                UPDATE contact_categories
                SET name = @name, color = @color
                WHERE id = @id AND is_default = FALSE";

            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@name", GroupName.Trim());
            command.Parameters.AddWithValue("@color", GroupColor);
            command.Parameters.AddWithValue("@id", GroupId);

            await command.ExecuteNonQueryAsync();

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteGroupAsync(int id)
        {
            using var connection = _db.CreateConnection();
            await connection.OpenAsync();

            const string sql = @"
                DELETE FROM contact_categories
                WHERE id = @id AND is_default = FALSE";

            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            await command.ExecuteNonQueryAsync();

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostExportAsync()
        {
            using var connection = _db.CreateConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT full_name, phone_number,
                       additional_phones, email, notes
                FROM contacts
                WHERE is_deleted = FALSE
                ORDER BY full_name";

            using var command = new MySqlCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();

            var csv = new StringBuilder();
            csv.AppendLine(
                "Full Name,Phone Number,Additional Phones,Email,Notes");

            while (await reader.ReadAsync())
            {
                string[] values =
                {
                    reader.GetString("full_name"),
                    reader.GetString("phone_number"),
                    reader.IsDBNull(reader.GetOrdinal("additional_phones"))
                        ? "" : reader.GetString("additional_phones"),
                    reader.IsDBNull(reader.GetOrdinal("email"))
                        ? "" : reader.GetString("email"),
                    reader.IsDBNull(reader.GetOrdinal("notes"))
                        ? "" : reader.GetString("notes")
                };

                csv.AppendLine(string.Join(",", values.Select(CsvEscape)));
            }

            return File(
                Encoding.UTF8.GetPreamble().Concat(
                    Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
                "text/csv",
                "calllect-contacts.csv");
        }

        private static string CsvEscape(string value)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public async Task<IActionResult> OnPostImportAsync()
        {
            if (CsvFile == null || CsvFile.Length == 0)
                return RedirectToPage();

            using var connection = _db.CreateConnection();
            await connection.OpenAsync();

            using var stream = new StreamReader(CsvFile.OpenReadStream());

            await stream.ReadLineAsync();

            while (true)
            {
                string? line = await stream.ReadLineAsync();

                if (line == null)
                    break;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',');

                if (parts.Length < 2)
                    continue;

                string name = parts[0].Trim().Trim('"');
                string phone = parts[1].Trim().Trim('"');

                if (string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(phone))
                    continue;

                const string sql = @"
                    INSERT INTO contacts
                    (full_name, phone_number)
                    SELECT @name, @phone
                    WHERE NOT EXISTS (
                        SELECT 1 FROM contacts
                        WHERE phone_number = @phone
                        AND is_deleted = FALSE
                    )";

                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@phone", phone);

                await command.ExecuteNonQueryAsync();
            }

            return RedirectToPage();
        }

        public class Contact
        {
            public int Id { get; set; }
            public string FullName { get; set; } = "";
            public string PhoneNumber { get; set; } = "";
            public string CountryCode { get; set; } = "+63";
            public string LocalPhoneNumber { get; set; } = "";
            public string AdditionalPhones { get; set; } = "";
            public string Email { get; set; } = "";
            public string Notes { get; set; } = "";
            public bool IsFavorite { get; set; }
            public string GroupNames { get; set; } = "";
        }

        public class Category
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Color { get; set; } = "#7048bd";
            public int SortOrder { get; set; }
            public bool IsDefault { get; set; }
        }
    }
}
