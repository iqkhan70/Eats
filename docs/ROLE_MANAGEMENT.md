# Role Management System

## Overview

The TraditionalEats platform supports a flexible multi-role system where users can have multiple roles simultaneously. This allows for scenarios like:
- **Standalone Customer**: User with only "Customer" role
- **Standalone Vendor**: User with only "Vendor" role  
- **Customer + Vendor**: User who can both place orders and manage restaurants
- **Admin**: Full system access

## Supported Roles

1. **Customer** - Can place orders, view menu, manage cart
2. **Vendor** - Can manage restaurants and menu items
3. **Admin** - Full system access, can manage users and roles
4. **Driver** - Can view and manage delivery assignments (future)

## How It Works

### Database Structure

- **Users** can have multiple **UserRoles**
- Each **UserRole** links a User to a Role
- Roles are stored in the **Roles** table
- The system uses a many-to-many relationship (User ↔ UserRole ↔ Role)

### Key Features

1. **Multiple Roles Per User**: A user can have Customer, Vendor, and Admin roles simultaneously
2. **Role Assignment**: Admins can assign roles to any user
3. **Role Revocation**: Admins can revoke roles from users (except the last role - users must have at least one role)
4. **Automatic UI Adaptation**: The UI automatically shows/hides features based on user roles

## Admin Role Management

### Web App

1. Navigate to **Admin Dashboard** → **User Management**
2. Enter the user's email address
3. Click **Load User Roles** to see current roles
4. To assign a role:
   - Select a role from the dropdown
   - Click **Assign Role**
5. To revoke a role:
   - Click **Revoke** next to the role badge

### API Endpoints

#### Assign Role
```
POST /api/auth/assign-role
Authorization: Bearer <admin_token>
Body: { "email": "user@example.com", "role": "Vendor" }
```

#### Revoke Role
```
POST /api/auth/revoke-role
Authorization: Bearer <admin_token>
Body: { "email": "user@example.com", "role": "Vendor" }
```

#### Get User Roles
```
GET /api/auth/user-roles/{email}
Authorization: Bearer <admin_token>
```

### BFF Endpoints (Web App)

- `POST /api/WebBff/admin/users/assign-role`
- `POST /api/WebBff/admin/users/revoke-role`
- `GET /api/WebBff/admin/users/{email}/roles`

## Common Scenarios

### Scenario 1: Customer Wants to Become a Vendor

1. Customer registers and gets "Customer" role automatically
2. Admin assigns "Vendor" role to the customer
3. Customer now has both "Customer" and "Vendor" roles
4. Customer can:
   - Place orders (Customer role)
   - Manage restaurants (Vendor role)
   - Access Vendor Dashboard

### Scenario 2: Revoke Vendor Status

1. Admin navigates to User Management
2. Enters the user's email
3. Clicks "Revoke" next to the "Vendor" role
4. User loses vendor access but keeps customer access

### Scenario 3: Standalone Vendor

1. User registers with "Vendor" role (or admin assigns it)
2. User does NOT have "Customer" role
3. User can manage restaurants but cannot place orders
4. If they want to place orders, admin assigns "Customer" role

## Security Considerations

1. **Role Assignment**: Only Admins can assign/revoke roles
2. **Last Role Protection**: Users cannot have all roles removed (must have at least one)
3. **JWT Token**: Roles are included in JWT tokens and validated on each request
4. **Authorization**: All endpoints use `[Authorize(Roles = "...")]` attributes

## UI Behavior

### Navigation Menu

- **Vendor Dashboard**: Shows if user has "Vendor" or "Admin" role
- **Admin Dashboard**: Shows if user has "Admin" role
- **Orders**: Shows for all authenticated users (Customer role implied)

### Vendor Dashboard

- Accessible if user has "Vendor" or "Admin" role
- Shows restaurants owned by the user
- Allows creating/editing restaurants and menu items

### Admin Dashboard

- Accessible only with "Admin" role
- Provides user management, restaurant management, and system settings

## Implementation Details

### Registration

When a user registers, they automatically get the "Customer" role unless specified otherwise:

```csharp
await _authService.RegisterAsync(email, phoneNumber, password, "Customer");
```

### Login

On login, all user roles are included in the JWT token:

```csharp
foreach (var userRole in user.UserRoles)
{
    claims.Add(new Claim(ClaimTypes.Role, userRole.Role.Name));
}
```

### Authorization Checks

```csharp
[Authorize(Roles = "Vendor,Admin")]  // Vendor OR Admin
[Authorize(Roles = "Admin")]         // Admin only
```

## Best Practices

1. **Default Role**: Always assign "Customer" role on registration
2. **Role Verification**: Check user roles before showing UI elements
3. **Error Handling**: Handle cases where users lose roles (e.g., redirect from vendor dashboard)
4. **Audit Trail**: Consider logging role changes for security (future enhancement)

## Future Enhancements

- [ ] Role-based permissions (fine-grained control)
- [ ] Role assignment history/audit log
- [ ] Bulk role management
- [ ] Role expiration dates
- [ ] Temporary role assignments
