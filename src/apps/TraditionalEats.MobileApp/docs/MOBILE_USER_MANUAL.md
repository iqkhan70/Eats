# Kram Mobile App – User Manual (Customers & Vendors)

This manual covers the **customer** (diner) and **vendor** (restaurant) experience in the Kram mobile app. It does **not** cover admin features.

---

## Table of contents

1. [Getting started](#1-getting-started)
2. [Customer: Browsing & ordering](#2-customer-browsing--ordering)
3. [Customer: Cart & checkout](#3-customer-cart--checkout)
4. [Customer: Orders & chat](#4-customer-orders--chat)
5. [Customer: Profile & account](#5-customer-profile--account)
6. [Vendor: Dashboard & restaurants](#6-vendor-dashboard--restaurants)
7. [Vendor: Menu management](#7-vendor-menu-management)
8. [Vendor: Orders & payments](#8-vendor-orders--payments)
9. [Vendor: Documents](#9-vendor-documents)

---

## 1. Getting started

### 1.1 App overview

The app has five main tabs at the bottom:

| Tab       | Purpose |
|----------|---------|
| **Home** | Discover vendors, search, and browse by category or location. |
| **Vendors** | List of vendors with search and filters. |
| **Cart** | Your shopping cart and checkout. |
| **Orders** | Your order history (requires sign-in). |
| **Profile** | Sign in, sign up, account settings, and (if applicable) Vendor Dashboard. |

You can browse and add items to the cart without signing in. You must **sign in** to place an order and to see your orders.

### 1.2 Sign up (create account)

1. Open the **Profile** tab.
2. Tap **Don't have an account? Sign Up**.
3. Enter:
   - First name, last name  
   - Display name (optional)  
   - Email  
   - Phone number (required for order notifications)  
   - Password (at least 6 characters)  
   - Confirm password  
4. Tap **Sign Up**.  
5. After success, you are signed in and can place orders and use Orders and Profile.

### 1.3 Sign in

1. Open the **Profile** tab.
2. Tap **Sign In**.
3. Enter your **email** and **password**.
4. Tap **Sign In**.  
   You are taken to the main app (Home). If your account has vendor access, you will see **Vendor Dashboard** under **Business** in Profile.

### 1.4 Forgot password

1. On the **Sign In** screen, tap **Forgot password?** (or open Forgot Password from the link on the login screen).
2. Enter your **email**.
3. Tap the button to send the reset link.
4. Check your email and follow the link to set a new password.  
   The app will show a message that if an account exists for that email, a reset link was sent.

---

## 2. Customer: Browsing & ordering

### 2.1 Home screen

- **Welcome** section at the top.
- **Distance slider** – “Within X miles.” Adjust the slider (1–100 miles) to control how far from your location vendors are shown.  
  Location is used only if you allow it; otherwise use search or categories.
- **Popular categories** – Tap a category (e.g. Traditional, Fast Food, Desserts, Vegetarian) to open the **Vendors** tab filtered by that category.
- **Nearby vendors** – If location is allowed, a list of vendors within the selected distance is shown. Tap a vendor to open its **menu**. Tap **View All** to see the full list on the Vendors tab with the same distance filter.
- **Search bar (bottom)** – Type a **ZIP code**, **address**, or **vendor/cuisine name**, then search. Results open on the Vendors tab (or you may go straight to a vendor/menu depending on the result).

### 2.2 Vendors tab

- **Search** – Use the bottom search bar to search by vendor name, cuisine, or location (e.g. ZIP or address). Suggestions may appear as you type.
- **Filters** – If you came from Home with a category or location, the active filters appear at the top (e.g. “Category: Italian”, “Within 25 mi”). Tap **Clear** to remove filters and see all vendors.
- **List** – Each card shows vendor name, cuisine type, address, distance (if location is set), and rating/review count. Tap a card to open that vendor’s **menu**.

### 2.3 Vendor menu

- **Tabs** – **Menu** and **Reviews**.
- **Menu tab**
  - Categories (if any) to filter items.
  - Each item shows name, description, price, and dietary tags.  
  - **Add to cart** (+ button) to add one. Use **−** / **+** to change quantity, then add.  
  - If the vendor has another restaurant in your cart, you may be prompted to clear the cart or replace it (one restaurant per cart).
- **Reviews tab**
  - Average rating and number of reviews.
  - List of reviews. If you have a delivered/completed order from this vendor and haven’t reviewed yet, you can **Write a review** (rating + optional text).

From the menu screen you can go to **Cart** (tab or button) or back to Vendors/Home.

---

## 3. Customer: Cart & checkout

### 3.1 Cart tab

- **Empty cart** – Message “Your cart is empty” and a **Browse Vendors** button. Pull down to refresh.
- **With items**
  - List of items with name, unit price, quantity controls (− / +), line total, and remove (trash).
  - **Subtotal**, **Tax**, **Delivery fee**, **Service fee**, and **Total**.
  - **Special instructions** – Optional (e.g. “Cut with clean knife”, “Pick up around 3 PM”).
  - **Delivery address** – Shown (currently often set to pickup-only text; you may not be able to edit it in-app depending on configuration).
  - **Place Order** – Tapping it checks sign-in and payment setup (see below).

### 3.2 Placing an order

1. Ensure **delivery address** (or pickup info) is correct.
2. Add any **special instructions** if you want.
3. Tap **Place Order**.
4. **If you are not signed in** – You’ll be prompted to sign in; after signing in, return to Cart and tap **Place Order** again.
5. **If the restaurant is not set up for payments** – A message explains that this restaurant can’t accept payments in the app yet; contact the restaurant directly.
6. **If payment is required (e.g. Stripe)** – The app may open a browser to complete payment. Finish payment in the browser, then return to the app. You’ll be directed to **Orders**.
7. **Success** – You see a confirmation (with short order ID). You can open **Orders** to see status and details.

You can **Clear cart** to remove all items. Pull down on the cart screen to refresh.

---

## 4. Customer: Orders & chat

### 4.1 Orders tab

- **Sign-in required** – If you’re not signed in, you’re redirected to **Sign In**.
- **Filters** – **All** | **Active** | **Past orders**.
  - **Active** – Orders not yet Delivered/Cancelled/Refunded.
  - **Past orders** – Delivered, Cancelled, or Refunded.
- **Search** – Use the bottom search bar to search by order ID, status, items, or address. Suggestions may include order IDs and statuses.
- **Order cards** – Each shows short order ID, date, first few items, status badge, and total. Tap a card to open **Order details**.

### 4.2 Order details

- **Details** – Full order info: items, quantities, prices, subtotal, tax, fees, total, delivery address, special instructions, status, and status history.
- **Chat** – Button to open **Order chat** to message the vendor about this order.
- **Reorder** – For past orders, you may see **Reorder** to add the same items to the cart (for the same or current menu).
- **Review** – For **Delivered** or **Completed** orders, a **Review** tab may appear so you can submit or edit a star rating and optional text.

### 4.3 Order chat

- Opened from the order details screen (e.g. “Chat” or message icon).
- Shows messages between you and the vendor for that order.
- Type in the input and send. Use the back button to return to order details or Orders.

---

## 5. Customer: Profile & account

### 5.1 Profile tab (signed out)

- **Guest** – “Sign in to access your account.”
- **Sign In** – Opens the sign-in screen.
- **Don't have an account? Sign Up** – Opens registration.

### 5.2 Profile tab (signed in)

- **Welcome** and your **email** at the top.
- **Business** (only if your account has vendor or admin role):
  - **Vendor Dashboard** – Opens the vendor area (see Vendor sections below).  
  (Admin options are not described in this manual.)
- **Account** – Links to:
  - Personal Information  
  - Addresses  
  - Payment Methods  
  - Notifications  
  - Help & Support  
  - Settings  
  (Some of these screens may be placeholders depending on app version.)
- **Sign Out** – Confirms and signs you out.

---

## 6. Vendor: Dashboard & restaurants

### 6.1 Accessing the vendor area

- Sign in with an account that has **vendor** role.
- Open **Profile** → **Vendor Dashboard**.  
  If you don’t see it, your account is not a vendor.

### 6.2 Vendor dashboard (“My Vendors”)

- **Header** – Back, “My Vendors”, and icons for **Documents**, **Orders**, and **Add** (new restaurant).
- **Stripe banner** – If Stripe setup is not complete, a yellow banner explains you need to finish Stripe Connect so you can accept paid orders. Tap **Finish Stripe setup** to open the Stripe onboarding link in the browser. In test mode you can use Stripe test data.
- **Restaurant list** – Each card shows:
  - Name and status (**Active** / **Inactive**)
  - Description and cuisine type
  - **Edit** – Edit restaurant details.
  - **Menu** – Manage menu items.
  - **Orders** – View orders for this restaurant.
  - **Delete** – Delete the restaurant (with confirmation).
- **FAB (+)** – Floating button to **Create vendor** (add a new restaurant).

### 6.3 Create restaurant

1. From the dashboard, tap **Add** (header) or **Create Vendor** (when you have no restaurants) or the **+** FAB.
2. Fill in:
   - **Name** (required)
   - **Address** (required)
   - Description, cuisine type, phone, email, image URL (optional)
3. Tap **Save**.  
   You’re returned to the dashboard and the new restaurant appears. You can then add menu items via **Menu**.

### 6.4 Edit restaurant

1. On the dashboard, tap **Edit** on a restaurant card.
2. Update name, address, description, cuisine type, phone, email, image URL, etc.
3. Tap **Save**.  
   Changes are saved and you return to the dashboard.

---

## 7. Vendor: Menu management

### 7.1 Open menu management

- From the vendor dashboard, tap **Menu** on a restaurant card.  
  You see the list of **menu items** for that restaurant.

### 7.2 Menu list

- **Add item** – Tap **Add item** (or +) to create a new menu item.
- **Pull to refresh** – Refresh the list.
- Each item shows name, price, category (if any), and availability. Tap an item to **edit** it, or use the edit control.

### 7.3 Add menu item

1. Tap **Add item** (or +).
2. Enter:
   - **Name** (required)
   - **Description** (optional)
   - **Price** (required)
   - **Category** (optional)
   - **Image URL** (optional)
   - **Available** – Toggle on/off (default on).
3. Tap **Save**.  
   The new item appears in the menu list and on the customer-facing menu when available.

### 7.4 Edit menu item

1. From the menu list, tap the item (or Edit).
2. Change name, description, price, category, image URL, or availability.
3. Tap **Save**.  
   Set **Available** to off to hide the item from customers without deleting it.

---

## 8. Vendor: Orders & payments

### 8.1 Viewing orders

- **From dashboard** – Tap **Orders** in the header to see all your orders, or tap **Orders** on a specific restaurant card to filter by that restaurant.
- **Orders screen** – List of orders with status, items, customer/address info (if shown), and total. Tap an order to open **order details**.

### 8.2 Updating order status (vendor)

- Open an order’s details.
- **Status** can be updated in sequence, for example:
  - **Pending** → **Preparing** or **Cancelled**
  - **Preparing** → **Ready** or **Cancelled**
  - **Ready** → **Completed** or **Cancelled**
  - **Completed** – No further status change.
  - **Cancelled** – No further status change.
- Select the new status and confirm (or use the control provided).  
  The customer sees the updated status in their Orders and order details.

### 8.3 Order chat (vendor)

- From the order details screen, open **Chat** (or the message icon).
- You can send and receive messages with the customer for that order.  
  Use this for questions about the order, pickup time, or special requests.

### 8.4 Payments (Stripe)

- To accept paid orders, complete **Stripe Connect** onboarding (see Stripe banner on the dashboard).
- After the customer places an order, they may be sent to Stripe Checkout in the browser. Once they pay and you mark the order **Completed**, the payment is captured according to your Stripe settings.
- If Stripe is not complete, customers may see that the restaurant is “not set up to accept payments yet” and be directed to contact you directly.

---

## 9. Vendor: Documents

- From the **Vendor dashboard** header, tap the **Documents** icon.
- This opens the **Documents** screen where you can upload or manage documents required for your vendor account (e.g. permits, insurance).  
  Exact fields and requirements depend on your region and app configuration.

---

## Quick reference

| I want to…                    | Where to go |
|--------------------------------|------------|
| Browse or search vendors       | Home or Vendors tab |
| View a vendor’s menu & add items| Vendors → tap vendor → Menu tab |
| See or edit my cart            | Cart tab |
| Place an order                 | Cart → Place Order (must be signed in) |
| See my orders                  | Orders tab (must be signed in) |
| Chat about an order            | Orders → order → Order details → Chat |
| Sign in / Sign up / Forgot password | Profile tab |
| Switch to vendor mode          | Profile → Vendor Dashboard |
| Manage my restaurants         | Vendor Dashboard |
| Manage menu items             | Vendor Dashboard → Menu on a restaurant |
| Manage orders (vendor)        | Vendor Dashboard → Orders (or Orders on a card) |
| Complete Stripe setup         | Vendor Dashboard → Finish Stripe setup (banner) |

---

*This manual applies to the Kram mobile app (customer and vendor roles only). Features and screens may vary slightly by version.*
