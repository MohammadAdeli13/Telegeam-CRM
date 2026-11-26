# syntax=docker/dockerfile:1.7
# =============================================================================
# Telegram CRM — Web image
# Multi-stage: Node 20 + pnpm (build) -> nginx (serve static assets)
# Build context: repository root
#   docker build -f infrastructure/docker/web.Dockerfile -t telegram-crm-web .
# =============================================================================

# ----------------------------- build stage -----------------------------------
FROM node:20-alpine AS build
WORKDIR /app

ENV CI=true \
    PNPM_HOME=/pnpm \
    PATH=/pnpm:$PATH

RUN corepack enable && corepack prepare pnpm@9 --activate

# Dependency layer: only the manifests, so installs are cached across source edits.
COPY frontend/package.json frontend/pnpm-lock.yaml* ./
RUN --mount=type=cache,target=/pnpm/store \
    pnpm install --frozen-lockfile

# Build layer
COPY frontend/ ./
ARG VITE_API_BASE_URL=/api/v1
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN pnpm build

# ----------------------------- runtime stage ---------------------------------
FROM nginx:1.27-alpine AS final

# SPA-aware nginx config (history fallback + long-lived asset caching)
COPY infrastructure/docker/web-nginx.conf /etc/nginx/conf.d/default.conf
# Shared security headers, included by every location block in web-nginx.conf
COPY infrastructure/docker/security-headers.conf /etc/nginx/conf.d/security-headers.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q --spider http://localhost/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
