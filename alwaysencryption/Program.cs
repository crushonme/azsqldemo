using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Azure.KeyVault;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using System.Data;
using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder();
builder.SetBasePath(Directory.GetCurrentDirectory())
       .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

IConfigurationRoot configuration = builder.Build();

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
string userAssignedClientId = configuration["MSIClientID"]!;
string SQLServerName = configuration["SQLServerName"]!;
string AkvUrl = configuration["AKVUrl"]!;
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
var credential = new DefaultAzureCredential(
    new DefaultAzureCredentialOptions
    {
        ManagedIdentityClientId = userAssignedClientId
    });
var secretClient = new SecretClient(new Uri(AkvUrl), credential);

var akvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(credential);

// Register the Azure Key Vault provider
SqlConnection.RegisterColumnEncryptionKeyStoreProviders(new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>
            {
                { SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, akvProvider }
            });

int iRetry = 0;
var connectionstring = $"Server=tcp:{SQLServerName}.database.windows.net,1433;Initial Catalog=testdb;Authentication=Active Directory Managed Identity; Encrypt=true; User Id={userAssignedClientId};TrustServerCertificate=False;Connection Timeout=30;Column Encryption Setting=enabled;Attestation Protocol=None;";
var keyInfo = new ConsoleKeyInfo();

do
{
    using (SqlConnection connection = new SqlConnection(connectionstring))
    {
        connection.Open();

        Console.WriteLine($"Start of Loop:{iRetry}; Press any key to continue");
        Console.ReadKey();
        // Prepare the SQL command
        SqlCommand cmd = connection.CreateCommand();

        cmd.CommandText = "SELECT [SSN], [FirstName], [LastName], [Salary] FROM [dbo].[Employees] WHERE [Salary] > @Salary";


        // Add parameter for the encrypted column
        SqlParameter ssnParam = new SqlParameter("@Salary", SqlDbType.Money);
        ssnParam.Value = "95000"; // Example SSN
        cmd.Parameters.Add(ssnParam);


        // Execute the command and read the results
        using (SqlDataReader reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                Console.WriteLine($"SSN: {reader["SSN"]}, Name: {reader["FirstName"]} {reader["LastName"]}, Salary: {reader["Salary"]}");
            }
        }
        connection.Close();
        Console.WriteLine($"End of Loop:{iRetry}; Press any key to retry, press ESC to exit");
        // Read the key
        keyInfo = Console.ReadKey(true); // true to not display the key in the console
        iRetry++;
    }

} while (keyInfo.Key != ConsoleKey.Escape);


Console.WriteLine($"Press any key to exit");
Console.ReadLine();