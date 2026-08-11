# 05 — Database Model (PostgreSQL)

## projects

- id uuid pk
- name varchar(160)
- instagram_handle varchar(160) null
- website_url text null
- manual_context text null
- current_step varchar(60)
- status varchar(40)
- created_at timestamptz
- updated_at timestamptz

`current_step`: sources, brand, campaign, strategy, ideas, content_review, creative_generation, creative_review, completed

## brand_sources

- id uuid pk
- project_id uuid fk
- type varchar(40)
- original_filename varchar(255) null
- source_url text null
- storage_key text null
- mime_type varchar(100) null
- extracted_text text null
- metadata_json jsonb null
- created_at timestamptz

Types: website, instagram_reference, screenshot, logo, image, pdf, manual, campaign_attachment

## brand_profiles

- id uuid pk
- project_id uuid fk unique
- profile_json jsonb
- confidence numeric(5,4)
- status varchar(30)
- created_at timestamptz
- updated_at timestamptz

Status: generated, approved

## campaigns

- id uuid pk
- project_id uuid fk
- name varchar(200)
- objective varchar(120)
- target_count int default 12
- period_start date null
- period_end date null
- context text null
- status varchar(40)
- created_at timestamptz
- updated_at timestamptz

## campaign_strategies

- id uuid pk
- campaign_id uuid fk unique
- strategy_json jsonb
- status varchar(30)
- created_at timestamptz
- updated_at timestamptz

Status: generated, approved

## content_ideas

- id uuid pk
- campaign_id uuid fk
- ordinal int
- title varchar(255)
- description text
- pillar varchar(80)
- content_type varchar(80)
- funnel_stage varchar(80)
- creative_angle varchar(100)
- score numeric(5,4)
- selected boolean default false
- idea_json jsonb
- created_at timestamptz

Unique `(campaign_id, ordinal)`.

## content_items

- id uuid pk
- campaign_id uuid fk
- source_idea_id uuid fk
- sequence int
- status varchar(40)
- current_revision_id uuid null
- approved_revision_id uuid null
- creative_brief_json jsonb null
- created_at timestamptz
- updated_at timestamptz

## content_revisions

- id uuid pk
- content_item_id uuid fk
- version int
- headline text
- supporting_text text null
- caption text
- cta text null
- hashtags_json jsonb null
- visual_direction text null
- instruction text null
- is_approved boolean default false
- created_at timestamptz

Unique `(content_item_id, version)`.

## creative_versions

- id uuid pk
- content_item_id uuid fk
- version int
- source_content_revision_id uuid fk
- layout_json jsonb
- background_storage_key text null
- image_storage_key text
- thumbnail_storage_key text null
- revision_instruction text null
- is_selected boolean default false
- created_at timestamptz

Unique `(content_item_id, version)`.

## jobs

- id uuid pk
- type varchar(100)
- entity_type varchar(100)
- entity_id uuid
- status varchar(40)
- progress int
- completed_steps int
- total_steps int
- message varchar(255) null
- error text null
- created_at timestamptz
- completed_at timestamptz null

## ai_runs

- id uuid pk
- operation varchar(100)
- entity_type varchar(100)
- entity_id uuid
- model varchar(100)
- input_json jsonb
- output_json jsonb null
- status varchar(40)
- error text null
- started_at timestamptz
- completed_at timestamptz null
