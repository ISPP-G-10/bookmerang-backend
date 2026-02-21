# 📚 Bookmerang Backend API

API REST desarrollada con **.NET 8** y **ASP.NET Core** para la gestión de libros de Bookmerang.

---

## 📋 Requisitos Previos

### Para ejecución local:
- **.NET 8 SDK** - [Descargar aquí](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Visual Studio 2022** (opcional, recomendado) o **VS Code**

### Para ejecución con Docker:
- **Docker Desktop** - [Descargar aquí](https://www.docker.com/products/docker-desktop)

---

## 🚀 Instalación y Ejecución Local

### 1️⃣ Clonar el repositorio
```bash
cd bookmerang-backend
```

### 2️⃣ Restaurar dependencias
```bash
cd Bookmerang.Api
dotnet restore
```

### 3️⃣ Compilar el proyecto
```bash
dotnet build
```

### 4️⃣ Ejecutar en modo desarrollo
```bash
dotnet run
```

La API estará disponible en:
- **HTTPS**: `https://localhost:5045`
- **HTTP**: `http://localhost:5044`
- **Swagger UI**: `http://localhost:5044/swagger`

---

## 🐳 Ejecución con Docker

### 1️⃣ Construir la imagen
Desde la carpeta raíz del backend (`bookmerang-backend`):
```bash
docker build -t bookmerang-api .
```

### 2️⃣ Ejecutar el contenedor
```bash
docker run -p 5044:8080 --name bookmerang-api bookmerang-api
```

### 3️⃣ Acceder a la API
- **API**: `http://localhost:5044`

### Comandos útiles:
```bash
# Ver contenedores corriendo
docker ps

# Ver logs
docker logs bookmerang-api

# Detener contenedor
docker stop bookmerang-api

# Eliminar contenedor
docker rm bookmerang-api

# Ejecutar en segundo plano
docker run -d -p 5044:8080 --name bookmerang-api bookmerang-api
```

---

## 📁 Estructura del Proyecto

```
Bookmerang.Api/
├── Controllers/           # Controladores de API
│   └── Auth/             # Endpoints de autenticación
├── Services/             # Lógica de negocio
│   ├── Implementation/   # Implementaciones de servicios
│   └── Interfaces/       # Interfaces de servicios
├── Models/               # Modelos de datos
├── Data/                 # Contexto de base de datos
├── Program.cs           # Configuración de la aplicación
└── appsettings.json     # Configuración
```

---

## 🔧 Configuración

### CORS
Configurado para aceptar peticiones desde:
- `http://localhost:8081` (Expo)
- `http://localhost:3000` (Web)

Editar en `Program.cs` para añadir más orígenes.

### Base de Datos
Por implementar. Actualizar `appsettings.json` con la cadena de conexión cuando esté lista.

---

## 🛠️ Desarrollo

### Agregar un nuevo servicio:
1. Crear interfaz en `Services/Interfaces/`
2. Crear implementación en `Services/Implementation/`
3. Registrar en `Program.cs`:
   ```csharp
   builder.Services.AddScoped<IServicio, Servicio>();
   ```

### Agregar un nuevo controlador:
1. Crear en `Controllers/`
2. Heredar de `ControllerBase`
3. Añadir atributos `[ApiController]` y `[Route]`

---

## 🧪 Testing

```bash
# Ejecutar tests
dotnet test

# Con cobertura
dotnet test --collect:"XPlat Code Coverage"
```

---

## 📝 Notas

- El proyecto usa **.NET 8.0**
- Swagger está habilitado solo en entorno de desarrollo
- Puerto por defecto: **5044** (HTTP) / **5045** (HTTPS)
- Puerto en Docker: **8080** (mapeado a 5044 en host)
