# DES104 - Desafío práctico 2: sistema distribuido con API Gateway

Universidad Don Bosco · Ciclo 02-2026 · Grupo 04L

Dos Web APIs en .NET 8 (Clientes y Pedidos) integradas mediante un API Gateway
construido con Ocelot, que funciona como punto único de entrada e implementa
autenticación, manejo centralizado de errores, respuestas estandarizadas,
registro de solicitudes y caché.

## Qué se instala en una PC nueva

1. **Visual Studio 2022** (edición Community sirve). En el instalador marque:
   - Carga de trabajo **ASP.NET y desarrollo web** (trae el SDK de .NET 8).
   - Carga de trabajo **Almacenamiento y procesamiento de datos**, que instala
     **SQL Server Express LocalDB**, la base de datos que usa la solución.

   Si Visual Studio ya está instalado sin esas cargas: abra *Visual Studio
   Installer* → *Modificar* → marque las dos → *Modificar*.

2. Verifique que LocalDB quedó disponible. En una terminal:
   ```
   sqllocaldb info mssqllocaldb
   ```
   Debe mostrar información de la instancia. Si dice que no existe:
   ```
   sqllocaldb create mssqllocaldb
   ```

3. **Postman** (o Bruno) para las pruebas. Opcional: Git para el repositorio.

No hace falta instalar nada más: Ocelot, Entity Framework e Identity llegan como
paquetes NuGet al restaurar la solución.

## Arquitectura

```
Cliente HTTP  ──►  API Gateway :5000  ──┬──►  ClientesAPI :5101  ──►  DesafioClientesDb
                   (Ocelot + Identity)  └──►  PedidosAPI  :5102  ──►  DesafioPedidosDb
                                                    │
                                                    └─ valida el cliente contra ClientesAPI
```

| Proyecto | Puerto | Base de datos |
|---|---|---|
| ApiGateway | 5000 | DesafioAuthDb (usuarios) |
| ClientesAPI | 5101 | DesafioClientesDb |
| PedidosAPI | 5102 | DesafioPedidosDb |

## Cómo ejecutarlo

1. Abrir `DesafioPractico2.sln` y restaurar los paquetes NuGet
   (clic derecho en la solución → *Restaurar paquetes NuGet*).

2. Generar las migraciones. En *Herramientas → Administrador de paquetes NuGet →
   Consola del Administrador de paquetes*, cambiando el **Proyecto predeterminado**
   en cada bloque:

   ```powershell
   # Proyecto predeterminado: ClientesAPI
   Add-Migration MigracionInicial
   Update-Database

   # Proyecto predeterminado: PedidosAPI
   Add-Migration MigracionInicial
   Update-Database

   # Proyecto predeterminado: ApiGateway
   Add-Migration MigracionIdentity
   Update-Database
   ```

   (Si no genera migraciones, cada API crea su esquema sola en el primer arranque.
   Lo que no conviene es arrancar primero y agregar migraciones después.)

3. Clic derecho en la solución → *Configurar proyectos de inicio* → *Varios
   proyectos de inicio* → poner los tres en **Iniciar**.

4. Ejecutar. En el primer arranque cada API siembra sus datos de prueba y lo
   informa en la consola.

5. En Postman, importar `tests/DES104-DesafioPractico2.postman_collection.json` y
   ejecutar primero **Login**.

## Autenticación

El Gateway usa ASP.NET Core Identity con cookie de sesión. Al arrancar crea los
roles y este usuario:

| Correo | Contraseña |
|---|---|
| admin@udb.edu.sv | Desafio2026* |

```http
POST http://localhost:5000/auth/login
Content-Type: application/json

{ "email": "admin@udb.edu.sv", "password": "Desafio2026*" }
```

Postman guarda la cookie y la reenvía automáticamente. Ocelot la valida en cada
ruta antes de reenviar la solicitud. Sin sesión, cualquier ruta responde 401 con
el formato estándar de la solución.

Endpoints propios del Gateway: `POST /auth/login`, `POST /auth/register`,
`POST /auth/logout`, `GET /auth/me`, `GET /health` y `GET /swagger`.

## Rutas del Gateway

Todo se consume desde `http://localhost:5000`.

| Método | Ruta | API interna | Caché |
|---|---|---|---|
| GET | /clientes | GET /api/clientes | no |
| GET | /clientes/{id} | GET /api/clientes/{id} | no (máx. 20 solicitudes/minuto) |
| POST | /clientes | POST /api/clientes | no |
| PUT | /clientes/{id} | PUT /api/clientes/{id} | no |
| DELETE | /clientes/{id} | DELETE /api/clientes/{id} | no |
| GET | /pedidos | GET /api/pedidos | **sí, 30 s** |
| GET | /pedidos-sin-cache | GET /api/pedidos | no (para comparar tiempos) |
| GET | /pedidos/{id} | GET /api/pedidos/{id} | **sí, 30 s** |
| GET | /pedidos/cliente/{clienteId} | GET /api/pedidos/cliente/{clienteId} | **sí, 30 s** |
| POST | /pedidos | POST /api/pedidos | no |
| PUT | /pedidos/{id} | PUT /api/pedidos/{id} | no |

Filtros disponibles: `/clientes?buscar=texto&activo=true&pagina=1&tamanoPagina=20`
y `/pedidos?estado=Entregado&clienteId=25`.

## Caché

Configurada en `ocelot.json` con el mecanismo propio de Ocelot:

```json
"CacheOptions": {
  "TtlSeconds": 30,
  "Region": "pedidos"
}
```

Se cachean solo las consultas de pedidos. Las de clientes no, porque es la
entidad que se crea, modifica y elimina en la demostración y los cambios deben
verse de inmediato.

## Siembra de datos

Los datos de prueba se generan con un ciclo `for` dentro de cada API:

- `src/ClientesAPI/Data/SembradorClientes.cs` → 1,500 clientes
- `src/PedidosAPI/Data/SembradorPedidos.cs` → 1,500 pedidos repartidos entre esos clientes

Solo se ejecuta cuando la tabla está vacía. Para cambiar la cantidad, modifique la
constante del inicio de cada archivo (`CantidadClientes` / `CantidadPedidos`). Si
sube la de clientes, ajuste también `CantidadClientes` en el sembrador de pedidos
para que ningún pedido quede apuntando a un cliente inexistente.

Para volver a sembrar: borre las filas o la base de datos y vuelva a arrancar.

## Pruebas de rendimiento

No hace falta ninguna herramienta extra: Postman muestra el tiempo de cada
solicitud junto al código de respuesta, y la consola del Gateway registra los
milisegundos de cada una.

1. Ejecute `GET /pedidos-sin-cache` cinco veces y anote los tiempos.
2. Ejecute `GET /pedidos` cinco veces seguidas. La primera será lenta (la caché
   está vacía) y las siguientes mucho más rápidas.
3. Espere 30 segundos y repita `GET /pedidos`: vuelve a ser lenta porque expiró
   el TTL, lo que confirma que la caché funciona.

Llene esta tabla con sus resultados para el informe:

| Solicitud | Sin caché (ms) | Con caché (ms) |
|---|---|---|
| 1ª | | |
| 2ª | | |
| 3ª | | |
| 4ª | | |
| 5ª | | |
| Promedio | | |

## Estructura

```
DesafioPractico2/
├── DesafioPractico2.sln
├── src/
│   ├── ApiGateway/      Ocelot, Identity y los dos middlewares del Gateway
│   ├── ClientesAPI/     CRUD de clientes + sembrador
│   └── PedidosAPI/      Pedidos + comunicación con ClientesAPI + sembrador
└── tests/               Colección de Postman y archivo .http
```

## Problemas comunes

| Síntoma | Solución |
|---|---|
| 502 al llamar /clientes o /pedidos | Alguna API interna no está corriendo. Verifique que los tres proyectos estén iniciados. |
| 401 después de iniciar sesión | Postman no está reenviando la cookie. Revise que las cookies estén habilitadas para localhost y que no haya llamado a /auth/logout. |
| No resuelve el paquete Ocelot.Cache.CacheManager | Abra *Administrar paquetes NuGet* en el proyecto ApiGateway, busque `Ocelot.Cache.CacheManager` e instale la versión más reciente compatible con Ocelot 25. |
| Error de conexión a SQL Server | Revise la cadena de conexión y que LocalDB exista (`sqllocaldb info mssqllocaldb`). |
| Update-Database dice que la tabla ya existe | La base se creó sola en un arranque previo. Ejecute `Drop-Database` en ese proyecto y repita `Update-Database`. |
| La siembra no corre | Solo corre con la tabla vacía. Borre las filas o la base de datos. |
| 429 en /clientes/{id} | Es el límite de 20 solicitudes por minuto de esa ruta. Se ajusta en `ocelot.json`. |
