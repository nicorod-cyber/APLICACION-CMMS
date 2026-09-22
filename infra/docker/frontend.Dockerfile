FROM node:22.23.2-alpine3.23@sha256:72c5815a06aed9a2273aea5628d74d348af57843a7b547af2fe53dd3e4b95261 AS build
WORKDIR /app

COPY frontend/package.json ./
COPY frontend/package-lock.json ./
RUN npm ci

COPY frontend ./
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.30.4-alpine3.24@sha256:adf5042a17f4ecdd200c595fa9ffd1be37efb18f89a830bd1a00e4ab4d59d42c AS runtime
COPY infra/nginx/frontend.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
