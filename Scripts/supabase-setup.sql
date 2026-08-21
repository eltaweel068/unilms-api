-- =====================================================
-- UniLMS: Supabase Initial Setup Script
-- Run this in Supabase SQL Editor BEFORE first launch
-- =====================================================

-- 1. Enable pgvector extension (for AI embeddings)
CREATE EXTENSION IF NOT EXISTS vector;

-- 2. Verify pgvector is enabled
SELECT * FROM pg_extension WHERE extname = 'vector';

-- =====================================================
-- NOTE: The tables below are created automatically
-- by EF Core migrations. This script only sets up
-- extensions and optional manual configurations.
-- =====================================================

-- 3. (Optional) Create an admin user manually
-- Replace the password hash with a BCrypt hash of your password
-- You can generate one at: https://bcrypt-generator.com/

-- INSERT INTO "Users" ("Id", "Name", "Email", "PasswordHash", "Role", "CreatedAt")
-- VALUES (
--   gen_random_uuid(),
--   'Admin User',
--   'admin@university.edu',
--   '$2a$11$...',  -- BCrypt hash of your password
--   1,             -- 1 = Admin
--   NOW()
-- );

-- =====================================================
-- Supabase Storage Setup (do this in the Dashboard):
--
-- 1. Go to Storage → New Bucket
-- 2. Name: "course-materials"
-- 3. Toggle "Public bucket" = ON
-- 4. Click "Create bucket"
--
-- Then add this RLS policy for the bucket:
-- =====================================================

-- Allow public read access to course materials
-- (Go to Storage → course-materials → Policies → New Policy)
--
-- Policy name: "Public Read Access"
-- Operation: SELECT
-- Policy: true
--
-- This allows enrolled students to download files via public URLs.
-- Upload/Delete is handled server-side via ServiceRoleKey (bypasses RLS).
