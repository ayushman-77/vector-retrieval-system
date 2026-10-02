# Vector Retrieval System

A full-stack Retrieval-Augmented Generation (RAG) application for document intelligence, designed to extract actionable insights from PDF and TXT files.

## Architecture

*   **Frontend**: Angular 18+, Standalone components, responsive dark-mode UI with glassmorphism.
*   **Backend**: C# ASP.NET Core 8 Web API.
*   **Relational Database**: Microsoft SQL Server (via Entity Framework Core). Stores document metadata, chunks, and query logs.
*   **Vector Database**: Qdrant. Stores vector embeddings for semantic search.
*   **AI Integration**: **Ollama** running locally (100% free and private) for embeddings (`nomic-embed-text`) and chat completions (`llama3.1`).

## Features

1.  **Document Ingestion Pipeline**: Upload `.pdf` or `.txt` files. The system automatically extracts the text, chunks it using a sliding window approach, and generates embeddings.
2.  **Vectorization & Storage**: Embeddings are stored in Qdrant, while the original text chunks and document metadata are stored in SQL Server.
3.  **RAG Search Engine**: User queries are vectorized and matched against Qdrant using Cosine Similarity. Top results are pulled from SQL Server and injected into the LLM system prompt for grounded answers.
4.  **Function Calling Mechanism**: Integrated tool calling (`query_sql_metadata`) allowing the LLM to autonomously trigger backend SQL queries for data aggregation.

## 🚀 Setup & Local Deployment

### Prerequisites
- **Docker Desktop** (must be installed and actively running on your machine)

### 1. Run with Docker Compose
To launch the entire stack (SQL Server, Qdrant Vector DB, Ollama, .NET Backend, Angular Frontend), run:

```bash
docker-compose up --build -d
```

### 2. Download the Local AI Models
Since we are using Ollama, you need to tell the Ollama container to download the models we configured (`llama3.1` and `nomic-embed-text`). Once Docker is running, open a new terminal and run:

```bash
docker exec -it vector-retrieval-system-ollama-1 ollama pull llama3.1
docker exec -it vector-retrieval-system-ollama-1 ollama pull nomic-embed-text
```
*(Note: the container name might vary depending on your folder name. You can use `docker ps` to find the exact name of the Ollama container).*

### 3. Access the Application
*   **Frontend Dashboard**: http://localhost:4200
*   **Backend Swagger UI**: http://localhost:5000/swagger
*   **Qdrant Dashboard**: http://localhost:6333/dashboard

## 🔮 Next Steps
*   Update `backend/Services/LLMService.cs` to integrate a real SQL Query executor for the function calling tool if full schema introspection is required.
*   Add authentication (JWT) for enterprise-grade security.
