FROM node:20-alpine AS build
WORKDIR /app

COPY frontend/package.json ./
COPY frontend/package-lock.json ./
RUN npm ci

COPY frontend ./
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY infra/nginx/frontend.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
