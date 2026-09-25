using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests;

[Discriminator("bdgrz.test.authorization.unprotected", 1)]
sealed record UnprotectedCompositionRequest : IRequest;
