# Vector Retrieval System

An enterprise-grade, fully containerized Retrieval-Augmented Generation (RAG) system. This application allows users to upload documents (PDF/TXT) and instantly chat with them using local, privacy-first AI models. 

It features an asynchronous event-driven architecture to handle heavy AI processing in the background without blocking the user interface.

---

## 🛠️ Technology Stack

**Frontend**
* **Framework:** Angular 18 (Standalone Components)
* **Styling:** Custom CSS with Glassmorphism and Dark Mode UX
* **Web Server:** Nginx (for production Docker serving)

**Backend & Microservices**
* **API Engine:** C# ASP.NET Core 8 Web API
* **Message Broker:** Apache Kafka & Zookeeper (for asynchronous background jobs)
* **Background Worker:** .NET Hosted Services (Kafka Consumers)

**Databases**
* **Relational Database:** Microsoft SQL Server (Stores document metadata, chunks, and chat history logs)
* **Vector Database:** Qdrant (Stores 768-dimensional high-density vector embeddings)

**Artificial Intelligence**
* **Model Engine:** Ollama (Runs models 100% locally on CPU/GPU)
* **Embedding Model:** `nomic-embed-text`
* **Chat Model:** `llama3.2:1b` (Highly optimized for fast local CPU inference)

**DevOps & Deployment**
* **Containerization:** Docker & Docker Compose (7 inter-connected containers)
* **CI/CD:** GitHub Actions (Automated SSH deployment to Cloud Servers)

---

## ⚙️ How The Pipeline Works

### 1. Document Ingestion Pipeline (Asynchronous)
To ensure the UI remains lightning-fast, document vectorization is offloaded to a background event queue.
1. **Upload:** User drops a PDF into the Angular UI.
2. **Dispatch:** The C# API saves the raw file to disk, creates a database record, and publishes a `DocumentJobMessage` to an Apache Kafka topic.
3. **Acknowledge:** The API instantly returns a success response to the UI.
4. **Process (Background):** The Kafka Background Worker consumes the message, extracts the text using `PdfPig`, and splits it into 1000-word chunks with overlapping boundaries.
5. **Vectorize:** The chunks are sent to the local Ollama container (`nomic-embed-text`) to generate mathematical vector embeddings.
6. **Store:** The text chunks are saved to SQL Server, and their corresponding vectors are saved to Qdrant.

### 2. Agentic Retrieval & Chat Pipeline (Tool Calling)
The system leverages Llama 3.2's native **Function Calling** capabilities to act as an autonomous agent. Instead of a hardcoded search, the LLM intelligently routes queries to specific backend tools.
1. **Agent Routing:** The user's query is sent to the LLM along with a JSON schema of available tools (`search_documents`, `get_database_stats`).
2. **Decision Making:** The LLM autonomously decides whether to respond casually (e.g., "Hello!"), query the SQL database for system statistics, or execute a vector search for specific knowledge.
3. **Tool Execution:** The C# backend intercepts the LLM's `tool_calls` request, executes the corresponding C# function (e.g., querying Qdrant or Entity Framework), and feeds the live data back to the LLM.
4. **Final Generation:** The Agent synthesizes the tool results into a natural language response, and the UI automatically attaches any source document names.

---

## 🚀 Local Deployment

### Prerequisites
- **Docker Desktop** (must be installed, running, and allocated at least 8GB of RAM).

### 1. Start the System
Launch the entire 7-container stack using Docker Compose:
```bash
docker-compose up --build -d
```

### 2. Download the AI Models
Since the AI runs 100% locally and privately, you must download the models into your Ollama container on the first run:
```bash
docker exec -it vector-retrieval-system-ollama-1 ollama pull llama3.2:1b
docker exec -it vector-retrieval-system-ollama-1 ollama pull nomic-embed-text
```

### 3. Access the Dashboards
* **Frontend UI:** http://localhost:4200
* **Backend API Swagger:** http://localhost:5000/swagger
* **Qdrant Dashboard:** http://localhost:6333/dashboard

---

## ☁️ Cloud Deployment (CI/CD)

This project includes a fully automated GitHub Actions pipeline (`.github/workflows/deploy.yml`). 
Every time you push code to the `main` branch, GitHub will automatically log into your rented cloud server (AWS EC2, Hetzner, etc.) via SSH, pull the latest code, and rebuild the Docker containers.

**To enable this:**
Add the following secrets to your GitHub Repository (*Settings > Secrets and variables > Actions*):
* `SERVER_HOST`: The IP address of your server.
* `SERVER_USER`: The SSH username (e.g., `ubuntu` or `root`).
* `SERVER_SSH_KEY`: Your private SSH key for the server.
