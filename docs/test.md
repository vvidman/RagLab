# RagLab Technical Overview

## What is a RAG Pipeline

A Retrieval-Augmented Generation pipeline is a system that enhances a language model's responses by supplying it with relevant external documents at query time. The model does not need to memorize every fact during training — instead, it retrieves and reads the relevant information on demand.

The core advantage of RAG over fine-tuning is flexibility. You can update the document store without retraining the model. This makes RAG ideal for private knowledge bases, internal documentation, and frequently changing data sources.

## Pipeline Stages

The RagLab pipeline consists of five distinct stages, each with a clear responsibility.

**Document Loading** is the first stage. Raw files in various formats are read from disk and converted into a unified internal Document representation. Noise such as formatting markers, headers, and footers is removed during this step.

**Chunking** splits the cleaned document into smaller pieces called chunks. Chunks are typically between 256 and 1024 characters. An overlap of 50 to 100 characters is applied between consecutive chunks to prevent information loss at boundaries.

**Embedding** transforms each chunk into a high-dimensional float vector using an embedding model. Semantically similar chunks end up with vectors that point in similar directions in the vector space.

**Vector Storage** persists the embedded chunks. At query time, the user's question is also embedded, and the store returns the chunks whose vectors are closest to the query vector. In RagLab, proximity is measured using cosine similarity.

**Generation** takes the retrieved chunks as context and passes them to a language model alongside the original question. The model is instructed to answer based solely on the provided context.

## Chunking Strategy

RagLab uses a fixed-size character-based chunker. This approach has clear trade-offs.

The main advantage is simplicity — no tokenizer dependency, predictable chunk boundaries, and easy to reason about. The main disadvantage is that chunk boundaries may fall mid-sentence, slightly degrading coherence.

An overlap of 64 characters is applied by default. This means the last 64 characters of chunk N are prepended to chunk N+1. The overlap ensures that a sentence split across a boundary appears in full in at least one chunk.

## Vector Similarity

Cosine similarity is used to rank retrieved chunks. It measures the angle between two vectors rather than their absolute distance. This makes it scale-invariant — a longer document does not produce a louder or more dominant embedding simply because it contains more words.

The formula is straightforward. The dot product of the two vectors is divided by the product of their magnitudes. The result ranges from negative one (opposite directions) through zero (orthogonal, unrelated) to one (identical direction, maximum similarity).

## Technology Stack

RagLab is built on .NET 9 and C#. LlamaSharp loads GGUF-format models directly into memory, bypassing any HTTP layer. The embedding model and the generation model are separate instances with separate weights. Both are registered as singletons in the dependency injection container because model loading is expensive and must happen only once.
