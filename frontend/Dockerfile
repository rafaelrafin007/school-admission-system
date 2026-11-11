# Build stage
FROM node:22 AS build
WORKDIR /app

# Copy dependency files
COPY package.json package-lock.json ./
RUN npm ci

# Copy source and build
COPY . .
RUN npm run build

# Runtime stage
FROM nginx:alpine AS runtime
WORKDIR /usr/share/nginx/html

# Install gettext for envsubst (environment variable substitution in Nginx config)
RUN apk add --no-cache gettext

# Copy built static assets
COPY --from=build /app/dist .

# Copy Nginx configuration template
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

# Substitute ${FRONTEND_API_UPSTREAM} at startup, preserving Nginx request variables ($host, $uri, $scheme).
ENTRYPOINT ["/bin/sh", "-c", "envsubst '${FRONTEND_API_UPSTREAM}' < /etc/nginx/conf.d/default.conf > /tmp/nginx.default.conf && mv /tmp/nginx.default.conf /etc/nginx/conf.d/default.conf && exec nginx -g 'daemon off;'"]
