namespace BBF.Shared.Contracts.Integrations;

public record OllamaStatusDto(bool Online, List<string> Models);
