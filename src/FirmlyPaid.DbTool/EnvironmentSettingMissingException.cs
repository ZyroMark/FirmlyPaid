namespace FirmlyPaid.DbTool;

/// <summary>
/// Raised when a required environment setting is missing. Carries the setting's name only,
/// never its value, so a stack trace can never print a connection string or the pepper.
/// </summary>
public sealed class EnvironmentSettingMissingException(string settingName)
    : Exception($"""
        The environment setting '{settingName}' is not set.

        On Windows, run scripts\db-setup.ps1 instead, which reads your .env file. To set it
        by hand for one session:

          $env:{settingName} = '...'
        """);
