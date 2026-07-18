using Seed.Application.Abstractions;

namespace Seed.Infrastructure;

public class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
