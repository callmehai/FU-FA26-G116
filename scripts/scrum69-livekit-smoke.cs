#:project ../tests/TrustCabinet.Web.Tests/TrustCabinet.Web.Tests.csproj
#:property PublishAot=false

await TrustCabinet.Web.Tests.Meetings.MeetingLifecycleLiveKitSmoke.RunAsync();
