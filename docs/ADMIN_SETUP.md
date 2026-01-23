# Adding an Admin User

There are two ways to add an admin user to the system:

## Option 1: Register a New Admin User (Simplest)

Use the registration API endpoint with `Role: "Admin"`:

### Via API (curl):
```bash
curl -X POST http://localhost:5001/api/Auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@traditionaleats.com",
    "phoneNumber": "+1234567890",
    "password": "YourSecurePassword123!",
    "role": "Admin"
  }'
```

### Via Swagger:
1. Navigate to `http://localhost:5001/swagger`
2. Find the `POST /api/Auth/register` endpoint
3. Use this request body:
```json
{
  "email": "admin@traditionaleats.com",
  "phoneNumber": "+1234567890",
  "password": "YourSecurePassword123!",
  "role": "Admin"
}
```

## Option 2: Assign Admin Role to Existing User

If you already have a user account and want to make them an admin, you can use the admin management endpoint (requires admin authentication):

### Via API (curl):
```bash
# First, login as an existing admin to get a token
curl -X POST http://localhost:5001/api/Auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "existing-admin@traditionaleats.com",
    "password": "Password123!"
  }'

# Then use the token to assign admin role
curl -X POST http://localhost:5001/api/Auth/assign-role \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -d '{
    "email": "user@traditionaleats.com",
    "role": "Admin"
  }'
```

## Available Roles

- `Customer` - Regular customer (default)
- `Vendor` - Restaurant owner
- `Admin` - Administrator with full access
- `Driver` - Delivery driver

## Notes

- The registration endpoint currently allows anyone to register with any role. In production, you may want to restrict role assignment to admins only.
- Admin users have access to all endpoints marked with `[Authorize(Roles = "Admin")]`
- After registration, you can login with the admin credentials to access admin features
