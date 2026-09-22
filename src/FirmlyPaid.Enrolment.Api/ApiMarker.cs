namespace FirmlyPaid.Enrolment.Api;

/// <summary>
/// Marker type so tests can start this service in-process with WebApplicationFactory.
/// A named marker avoids the ambiguity of seven assemblies each exposing "Program".
/// </summary>
public sealed class ApiMarker;
