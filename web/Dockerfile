# syntax=docker/dockerfile:1
# Build context: web/ (see docker-compose.yml).

FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .
# Production bundles talk to the real API through the nginx proxy; MSW stays out.
ENV VITE_MOCK=false
RUN npm run build && rm -f dist/mockServiceWorker.js

FROM nginx:1.29-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=6 \
    CMD wget -q --spider http://127.0.0.1/healthz || exit 1
