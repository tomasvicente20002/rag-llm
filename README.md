# RagSuite

Solução de referência para fluxos RAG completos em .NET 8. Inclui:

- **RagBuilder**: aplicação de consola para ingerir ficheiros locais em colecções de conhecimento e gerir RAGs.
- **RagService**: API minimal que disponibiliza endpoints para gestão e chat com recuperação aumentada.
- **RagCore**: biblioteca partilhada com integrações de OpenAI, Qdrant, chunking e serviços reutilizáveis.

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download).
- Docker (para executar Qdrant).
- Conta e chave de API OpenAI com acesso aos modelos escolhidos.

## Configuração do Qdrant

```bash
docker run -p 6333:6333 -p 6334:6334 -v qdrant:/qdrant/storage qdrant/qdrant:latest
```

## Variáveis de ambiente

Configurar as variáveis antes de executar os projectos:

```bash
export OPENAI_API_KEY="..."
export OPENAI_EMBEDDING_MODEL="text-embedding-3-large"
export OPENAI_CHAT_MODEL="gpt-4o"
export QDRANT_ENDPOINT="http://localhost:6333"
```

Valores adicionais podem ser ajustados em `appsettings.json` de cada projecto.

## Estrutura da solução

```
RagSuite.sln
├─ RagCore/            Biblioteca partilhada
├─ RagBuilder/         CLI de ingestão
├─ RagService/         Minimal API
└─ tests/RagCore.Tests Testes unitários
```

## RagBuilder

Executar comandos a partir da raiz do repositório:

```bash
dotnet run --project RagBuilder -- ingest --rag "contratos" --path "./data/contratos" --chunk 800 --overlap 150 --tags juridico,2024

dotnet run --project RagBuilder -- list

dotnet run --project RagBuilder -- delete --rag "contratos"
```

A ingestão aceita pastas com ficheiros `.txt` e `.md`. Para ficheiros ZIP utilizar o endpoint `/ingest` da API.

## RagService

Iniciar a API:

```bash
dotnet run --project RagService
```

Endpoints principais:

- `GET /rags` – lista RAGs existentes com contagem de chunks.
- `POST /rags` – valida/cria identificadores lógicos.
- `DELETE /rags/{id}` – remove todos os chunks de um RAG.
- `POST /ingest` – ingere ficheiros a partir de caminho local (JSON) ou ZIP (`multipart/form-data`).
- `POST /chat` – aceita `application/json` (resposta imediata) ou `text/event-stream` com streaming de tokens e evento final `citations`.

Exemplo de chamada com SSE:

```bash
curl -N -H "Accept: text/event-stream" -H "Content-Type: application/json" \
  -d '{ "ragIds": ["contratos","financeiro-2024"], "query": "Qual é o prazo?", "topK": 6 }' \
  http://localhost:5187/chat
```

## Testes

```bash
dotnet test
```

Os testes actuais validam o chunker e o compositor de prompts.

## Extensões futuras

- Suporte a leitores `.pdf`/`.docx` via implementações adicionais de `ITextLoader`.
- Filtros por tags no endpoint de chat.
- Cache de embeddings baseada em hash dos textos.
