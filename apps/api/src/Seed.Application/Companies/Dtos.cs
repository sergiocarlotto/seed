namespace Seed.Application.Companies;

public record CreateCompanyRequest(string Name);
public record UpdateCompanyRequest(string Name);
public record CompanyDto(Guid Id, string Name, string Status, DateTime CreatedAt, DateTime UpdatedAt);
