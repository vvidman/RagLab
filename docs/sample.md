# RagLab

RagLab is an educational, hand-built RAG (Retrieval-Augmented Generation) pipeline implemented in .NET/C#.

## What is RAG?

Retrieval-Augmented Generation (RAG) is a technique that enhances large language model responses by first retrieving relevant documents from a knowledge base and then providing them as context to the model.

## Pipeline Stages

### Indexing

During indexing, documents are loaded from disk, split into chunks, embedded into dense vector representations, and stored in a vector store.

### Querying

During querying, the user question is embedded, the most similar chunks are retrieved from the vector store, and the chunks are passed as context to the language model to generate an answer.

## Components

- **IDocumentLoader** — loads raw text from files
- **IChunker** — splits documents into overlapping fixed-size chunks
- **IEmbedder** — produces float vector embeddings using a local GGUF model via LlamaSharp
- **IVectorStore** — stores and searches embedded chunks using cosine similarity
- **IGenerator** — generates natural language answers given a query and retrieved context
