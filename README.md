# OpenBomberNet Server Refatorado

Este repositório contém o código refatorado do servidor OpenBomberNet, incorporando melhorias como logging estruturado, persistência com MongoDB, suporte a Docker e uma arquitetura mais limpa.

## Pré-requisitos

*   **.NET 8 SDK** (ou a versão especificada nos arquivos `.csproj`): Necessário para compilar e rodar o projeto localmente.
*   **Docker**: Necessário para construir a imagem do servidor.
*   **Docker Compose**: Necessário para orquestrar os containers do servidor e do MongoDB.

## Configuração Inicial

1.  **Clonar o Repositório:** Clone este repositório para sua máquina local.
2.  **Adicionar Projeto Common à Solution:**
    *   Devido a limitações no ambiente de desenvolvimento remoto, o projeto `OpenBomberNet.Common` não pôde ser adicionado automaticamente ao arquivo `OpenBomberNet.sln` via linha de comando.
    *   **Abra a solution (`OpenBomberNet.sln`) no Visual Studio ou Rider:** Clique com o botão direito na Solution -> Add -> Existing Project... -> Navegue até `OpenBomberNet.Common/OpenBomberNet.Common.csproj` e adicione-o.
    *   **Alternativamente, use a CLI do .NET localmente:** Navegue até o diretório raiz do projeto no terminal e execute:
        ```bash
        dotnet sln OpenBomberNet.sln add OpenBomberNet.Common/OpenBomberNet.Common.csproj
        ```
3.  **Adicionar Referências ao Projeto Common:**
    *   Certifique-se de que os projetos que utilizam as constantes (principalmente `OpenBomberNet.Server`, `OpenBomberNet.Application`) tenham uma referência ao projeto `OpenBomberNet.Common`.
    *   No Visual Studio/Rider: Clique com o botão direito no projeto (ex: `OpenBomberNet.Server`) -> Add -> Project Reference... -> Marque `OpenBomberNet.Common`.
    *   Via CLI:
        ```bash
        dotnet add OpenBomberNet.Server/OpenBomberNet.Server.csproj reference OpenBomberNet.Common/OpenBomberNet.Common.csproj
        dotnet add OpenBomberNet.Application/OpenBomberNet.Application.csproj reference OpenBomberNet.Common/OpenBomberNet.Common.csproj
        # Adicione referências a outros projetos se necessário
        ```
4.  **Restaurar Dependências:**
    ```bash
    dotnet restore
    ```

## Executando com Docker Compose (Recomendado)

Este método irá construir a imagem do servidor e iniciar os containers do servidor e do MongoDB automaticamente.

1.  **Navegue até o diretório raiz do projeto** (onde o `docker-compose.yml` está localizado).
2.  **Execute o Docker Compose:**
    ```bash
    docker-compose up --build
    ```
    *   O `--build` garante que a imagem do servidor seja construída na primeira vez ou quando o código for alterado.
    *   O servidor estará acessível na porta `8888` (ou a porta configurada) e o MongoDB estará rodando internamente, com dados persistidos em um volume Docker (`mongodb_data`).
3.  **Para parar os containers:** Pressione `Ctrl+C` no terminal onde o compose está rodando, e depois execute:
    ```bash
    docker-compose down
    ```

## Estrutura do Projeto e Melhorias

*   **OpenBomberNet.Common:** Projeto centralizado para constantes de protocolo, delimitadores e outros elementos compartilhados.
*   **OpenBomberNet.Domain:** Contém as entidades de domínio (`Player`, `Map`, `Bomb`, etc.) e interfaces de repositório (`IRepository`, `IEntity`).
*   **OpenBomberNet.Application:** Contém a lógica de aplicação (serviços como `LobbyService`, `GameService`) e interfaces de serviços.
*   **OpenBomberNet.Infrastructure:** Implementações concretas para acesso a dados (`MongoRepository`), segurança (`SimpleAuthenticationService`), rede (`InMemoryConnectionManager`, que também atua como `IMessageSender`) e configurações (`MongoDbSettings`).
*   **OpenBomberNet.Server:** Ponto de entrada da aplicação (`Program.cs`), configuração de DI, o `TcpServer` (implementado como `IHostedService`), Handlers de mensagens e o `Dockerfile`.
*   **Logging:** `Console.WriteLine` foi substituído por `ILogger<T>` em todo o projeto para logging estruturado.
*   **Persistência:** O repositório em memória foi substituído por `MongoRepository`, utilizando MongoDB para persistir dados (atualmente configurado para `Player`, mas extensível).
*   **Docker:** Suporte completo via `Dockerfile` e `docker-compose.yml` para fácil execução e deploy.
*   **DI:** Configuração robusta de Injeção de Dependência no `Program.cs` utilizando o Host Genérico do .NET.

## Próximos Passos (Sugestões)

*   Implementar persistência para outras entidades (Mapas, estado do jogo).
*   Refinar a segurança (autenticação mais robusta, criptografia real).
*   Implementar testes unitários e de integração.
*   Expandir as funcionalidades do jogo.
*   Melhorar o tratamento de erros e a resiliência da conexão.

