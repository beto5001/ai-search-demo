namespace AzureBlobSearch.Application;

public static class SearchRequestPolicy
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    public static void Validate(string query, int page, int pageSize)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new SearchValidationException("O parâmetro 'q' é obrigatório.");
        }

        if (page < 1)
        {
            throw new SearchValidationException("A página deve ser maior ou igual a 1.");
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw new SearchValidationException(
                $"O tamanho da página deve estar entre 1 e {MaximumPageSize}.");
        }
    }
}

public sealed class SearchValidationException(string message) : Exception(message);

