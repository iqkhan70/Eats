-- Seed "Kurry Leaves" restaurant and its full menu
-- Owner: same as "Desi Bites" (926f872c-129d-42c1-8c78-02f4b6f23936)
-- Food category: c931f5f8-dd31-492a-9764-24ab1cbd1344

SET @restaurant_id = UUID();
SET @owner_id = '926f872c-129d-42c1-8c78-02f4b6f23936';
SET @food_cat = 'c931f5f8-dd31-492a-9764-24ab1cbd1344';
SET @now = UTC_TIMESTAMP(6);

-- 1. Create the restaurant
INSERT INTO traditional_eats_restaurant.Restaurants
  (RestaurantId, OwnerId, Name, Description, CuisineType, Address, Latitude, Longitude,
   PhoneNumber, Email, IsActive, Rating, ReviewCount, CreatedAt, UpdatedAt, VendorType)
VALUES
  (@restaurant_id, @owner_id, 'Kurry Leaves',
   'Authentic Indian & Pakistani cuisine featuring biryanis, tandoori specialties, curries, and more.',
   'Indian', '123 Main St, Overland Park, KS 66221',
   38.9717, -94.7014,
   '+1-555-0200', 'info@kurryleaves.com',
   1, 0, 0, @now, @now, 'Food');

SELECT @restaurant_id AS NewRestaurantId;

-- 2. Create all menu items
INSERT INTO traditional_eats_catalog.MenuItems
  (MenuItemId, RestaurantId, CategoryId, Name, Description, Price, IsAvailable, DietaryTagsJson, CreatedAt, UpdatedAt)
VALUES
-- === APPETIZERS / STARTERS ===
(UUID(), @restaurant_id, @food_cat, 'Vegetable Samosa (3 Pieces)', NULL, 6.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Vegetable Pakora', NULL, 7.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Tomato Soup', NULL, 4.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Kachomer Salad', NULL, 5.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Corn Manchurian', NULL, 10.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Vegetable Fried Rice', NULL, 11.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chili Paneer', NULL, 13.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Gobi Chilli', NULL, 10.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Gobi Manchurian', NULL, 10.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Channa Bathura', NULL, 14.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Corn Soup', NULL, 5.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Fried Rice', NULL, 13.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken 65', NULL, 13.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Mirchi Chicken', NULL, 13.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chilli Chicken', NULL, 13.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Lollipop', NULL, 13.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chili Shrimp', NULL, 14.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chili Fish', NULL, 14.99, 1, '[]', @now, @now),

-- === MEAT SPECIALTIES (Served with rice) ===
(UUID(), @restaurant_id, @food_cat, 'Chicken Curry', 'Tender boneless chicken cooked with a delicately spiced curry sauce. Served with rice.', 15.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Korma', 'Boneless chicken cooked in South Indian style with mixed fried onions. Served with rice.', 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Palak Chicken', 'Boneless chicken cooked with spinach and blended with spices. Served with rice.', 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Goat Sukha', 'Mouth watering dry preparation goat meat. Served with rice.', 18.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Gongura Goat', 'Pieces of goat cooked in south Indian Andhra style. Served with rice.', 18.50, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Goat Curry', 'Succulent pieces of goat made in north Indian style. Served with rice.', 18.50, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Achari Goat', 'Bone in goat cooked in pickle flavor with Indian spices. Served with rice.', 18.60, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Goat Kadai', 'Bone in goat cooked in Pakistani style. Served with rice.', 18.50, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Palak Ghosht', 'Bone in goat cooked with spinach and blended with spices. Served with rice.', 18.50, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Goat Tikka Masala', 'Okra with goat meat in Pakistani style. Served with rice.', 18.50, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Lamb Korma', 'Boneless lamb braised in a sauce of yogurt, cream, and nut. Served with rice.', 18.50, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Lamb Saag', 'Boneless pieces of lamb cooked with spinach and blended with spices. Served with rice.', 18.50, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Lamb Tikka Masala', 'Tender boneless meat grilled in tandoor cooked tomato cream sauce with imported seasoning. Served with rice.', 18.50, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Lamb Rogan Josh', 'Succulent pieces of lamb in a flavored Indian sauce. Served with rice.', 18.50, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Butter Chicken', 'Chicken pieces roasted in tandoor and then cooked in a creamy sauce. Served with rice.', 16.50, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Vandallo', 'Boneless chicken cooked in Goa Style. Served with rice.', 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Kalimirch', 'Boneless chicken in onions and yogurt sauce, flavored with peppercorns. Served with rice.', 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Masala', 'Boneless chicken cooked in chef special recipe. Served with rice.', 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Kadai', 'Boneless chicken cooked with tomatoes, onions, and bell peppers in North Indian style. Served with rice.', 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Tikka Masala', 'Tender boneless chicken grilled in tandoor cooked tomato cream sauce with imported seasonings. Served with rice.', 16.50, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Chettinad', 'Boneless chicken pieces cooked in southern style. Served with rice.', 17.00, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Achari Chicken', 'Boneless chicken cooked in pickle flavor with Indian spices. Served with rice.', 16.99, 1, '[]', @now, @now),

-- === BIRYANI (Served with raita) ===
(UUID(), @restaurant_id, @food_cat, 'Chicken Biryani', 'Rice cooked with chicken in Hyderabadi style. Served with raita.', 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Goat Biryani', 'Rice cooked with goat meat in Hyderabadi style. Served with raita.', 18.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Veg Biryani', 'Rice cooked with vegetables in Hyderabadi style. Served with raita.', 15.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chilli Paneer Biryani', 'Chef''s special cottage cheese with high spices biryani. Served with raita.', 16.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chatkhara Biryani', 'Chef special! Boneless chicken 65 biryani. Served with raita.', 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Mirchi Chicken Biryani', 'Chef special! Chilli chicken biryani. Served with raita.', 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Abbas Special Biryani', 'Authentic Chef''s special with boneless chicken and rich spices biryani. Served with raita.', 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Egg Biryani', 'Rice cooked with three eggs in South Indian Style. Served with raita.', 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Shrimp Biryani', 'Rice cooked with shrimp in South Indian Style. Served with raita.', 17.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Lamb Biryani', 'Chef''s special boneless biryani. Served with raita.', 18.99, 1, '[]', @now, @now),

-- === VEGETABLE SPECIALTIES (Served with rice) ===
(UUID(), @restaurant_id, @food_cat, 'Mutter Paneer', 'Cottage cheese with green peas and cooked creamy onion sauce. Served with rice.', 15.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Dal Makhani', 'Chef''s special! Black lentils cooked overnight in a creamy sauce. Served with rice.', 14.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Kansas Korma', 'Vegetables braised in a sauce made of yogurt, coconut milk, cream, and nut. Served with rice.', 15.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Spinach Kofta', 'Spinach balls cooked in a rich gravy. Served with rice.', 15.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Paneer Butter Masala', 'Cottage cheese cooked in buttery and silky tomato curry. Served with rice.', 15.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Paneer Tikka Masala', 'Cottage cheese cooked in tandoor with tomato cream sauce and imported seasonings. Served with rice.', 15.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Saag Paneer', 'Fresh spinach cooked with cottage cheese delicately spiced. Served with rice.', 15.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Malai Kofta', 'Cottage cheese and potato balls cooked in a flavored cream cashew sauce. Served with rice.', 15.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Dal Tadka', 'Our variation of Dal Tadka made with lentils and tempered with aromatic spices. Served with rice.', 14.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Channa Masala', 'Chickpeas cooked with ginger, onions, and tomatoes. Served with rice.', 14.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Mutter Methi Malai', 'Cottage cheese with methi flavor cooked in creamy onion sauce. Served with rice.', 15.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Shahi Paneer', 'Cottage cheese cooked in heavy rich creamy sauce. Served with rice.', 15.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Baingan Bharta', 'Roasted eggplant cooked in North Indian style. Served with rice.', 15.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Kadai Paneer', 'Paneer cubes cooked with onions and bell peppers in North Indian Style. Served with rice.', 15.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Okra Fry', 'Deep fried okra in South Indian Style. Served with rice.', 15.99, 1, '["Vegetarian"]', @now, @now),

-- === TANDOORI SPECIALTIES (Served with raita) ===
(UUID(), @restaurant_id, @food_cat, 'Tandoori Chicken', 'Bone in Tandoori chicken marinated overnight in herbs and spices. Served with raita.', 15.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Overland Park Malai Tikka', 'Boneless chicken tikkas marinated with subtle aromatic spices and grilled in tandoor. Served with raita.', 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Lahori Kebab', 'Chicken minced seekh kebab cooked in tandoor and mix Asian spices. Served with raita.', 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Abbas Special Barbeque Chicken', 'Boneless Thai meat marinated overnight Pakistani authentic spices and grilled in Tandoor. Served with raita.', 16.99, 1, '[]', @now, @now),

-- === BREADS ===
(UUID(), @restaurant_id, @food_cat, 'Naan', NULL, 2.50, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Roti', NULL, 3.50, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Garlic Naan', NULL, 3.50, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Onion Chili Kulcha', NULL, 5.00, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Garlic Roti', NULL, 3.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Roghni Naan', NULL, 3.99, 1, '["Vegetarian"]', @now, @now),

-- === DRINKS ===
(UUID(), @restaurant_id, @food_cat, 'Mango Lassi', NULL, 5.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Canned Soda', NULL, 2.00, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Masala Chai', NULL, 3.99, 1, '["Vegetarian"]', @now, @now),

-- === DESSERTS ===
(UUID(), @restaurant_id, @food_cat, 'Gulab Jamun', NULL, 4.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Rasmalai', NULL, 4.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Rice Kheer', NULL, 4.99, 1, '["Vegetarian"]', @now, @now);
