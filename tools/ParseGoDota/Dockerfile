# Usamos a imagem oficial do Golang baseada em Debian (mais compatível)
FROM golang:1.26-bookworm AS builder

# Instala ferramentas usando o comando padrão do Debian (apt-get)
RUN apt-get update && apt-get install -y git build-essential

WORKDIR /app

# Copia dependências e baixa
COPY go.mod go.sum ./
RUN go mod download

# Copia código e compila
COPY . .
RUN go build -o dota-parser ./cmd/parsegodota/main.go

# Estágio final (usamos uma imagem Debian slim para rodar o binário)
FROM debian:bookworm-slim
WORKDIR /root/
COPY --from=builder /app/dota-parser .

ENTRYPOINT ["./dota-parser"]