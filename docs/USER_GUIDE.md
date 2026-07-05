---
updated: 2026-07-05
commit: 72c9ef5
---

# Gatherstead User Guide

Welcome to Gatherstead — one place to keep your extended family's details together and to plan every shared weekend, holiday, or reunion.

This guide is organized by task. Every section and step is tagged with an **access badge** showing the lowest role that can use it, so you can jump straight to what applies to you.

## Roles at a glance

Gatherstead is organized into **groups** — each extended family or organization is its own group, and its data stays private to it. Within a group you have a **role**. Higher roles can always do everything the roles below them can:

| Role | Can do |
|---|---|
| **Owner** | Everything, plus manage the group itself. |
| **Manager** | Set up households, properties, accommodations, equipment, and invite/manage people. |
| **Coordinator** | Plan events — create and manage meal and task templates. |
| **Member** | Sign up for events, request stays, claim shopping items, view reports. |
| **Guest** | Limited, scoped access. |

Separately, each **household** has its own Manager/Member roles that control who can edit that household's people.

## How access is marked

Each section and sub-section below begins with an access badge naming the **lowest role** that can use it:

- 🟢 **Everyone** — anyone in the group, including Guests
- 🔵 **Member +** — Member, Coordinator, Manager, Owner
- 🟡 **Coordinator +** — Coordinator, Manager, Owner
- 🟠 **Manager +** — Manager, Owner

A few personal actions are also open to **you about yourself** (when your login is linked to your own member record) or to your **Household Manager**, even when your group role is lower — these are called out inline as *Self / Household Manager*.

<!--
Access tokens (stable — for future filtering/search tooling): everyone | member | coordinator | manager | owner.
Every section and sub-section starts with a "> **Access:**" blockquote using one of these levels
(a section may show a range). Badges live in the blockquote, never in the heading, so heading anchors stay clean.
-->

## 1. Getting started

> **Access:** 🟢 Everyone

You don't sign up for Gatherstead on your own — a group **Manager invites you by email** (see §2), and groups themselves are created by the platform administrator.

### Signing in and joining your group

> **Access:** 🟢 Everyone

1. Go to the Gatherstead site and choose **Sign In**. You'll sign in with your Microsoft account — there's no separate Gatherstead password.
2. On your first sign-in, your account is created automatically. If someone invited you, that invitation is matched to your verified email and you're added to their group right away — nothing else to accept.
3. You'll land on the **group picker**. Pick your group to enter the app. (No group yet? You'll see a short message with a way to get in touch — ask whoever runs your group to send you an invite.)

### Your account and profile

> **Access:** 🟢 Everyone

- Open the **account menu** (your name, bottom-left on desktop or top-right on mobile) → **Account** to set your **Display Name**.
- If a manager has linked your login to your record in the family directory, **Your Profile** also appears in the account menu — a shortcut straight to your own details.

## 2. Directory: households & members

> **Access:** 🟢 Everyone to browse · 🟠 Manager + to manage

The **Directory** is your family's address book: **households** (family units) and the **people** in them. A person's record — their name, birthday, and dietary needs — is separate from a login account; a login is *linked* to a person so the app knows "this is you."

### Browsing the directory

> **Access:** 🟢 Everyone

- View households and the people in them, and open **Your Profile** from the account menu.
- *Self / Household Manager:* if your login is linked to your own record, you can edit your own details (including your **dietary tags**, which feed meal planning and reports) from your profile page.

### Setting up households and members

> **Access:** 🟠 Manager + (or that household's Manager)

- **Create a household:** Directory → **Create**. Give it a name; notes and custom attributes are optional.
- **Add people:** open the household → **Create Member**. Enter at least a name; you can also record a birth date (or age band), dietary notes, and **dietary tags** (allergies, restrictions, or diets).

### Inviting people and managing access

> **Access:** 🟠 Manager +

- **Invite someone:** Settings → Users → **Invite**. Enter their email and a group role, and optionally attach them to a household with a household role. Existing users are added immediately; new users join automatically the next time they sign in — no email delivery needed on your end.
- **Link a login to a person:** in Settings → Users, open a user to set their **group role**, choose the **member record** they represent, and grant **household access** (Manager or Member).

## 3. Places & things: properties, accommodations, equipment

> **Access:** 🟢 Everyone to view · 🟠 Manager + to set up

These are the physical side of a gathering — where you meet, where people sleep, and what gear is on hand.

### Browsing places and equipment

> **Access:** 🟢 Everyone

- View properties, their accommodations, and the group's equipment, all read-only.

### Setting up properties

> **Access:** 🟠 Manager +

- **Properties → Create**, then give the place a name (notes and attributes optional). Open a property to edit it, delete it, or manage its accommodations.

### Setting up accommodations

> **Access:** 🟠 Manager +

Accommodations live **inside a property** — open the property's page and add them there:

- **Name** and **Type** (Bedroom, Bunk, RV pad, Tent, or Offsite).
- A **bed list** (size + how many of each) — Gatherstead uses this to work out how many people the spot sleeps.
- Optional room dimensions and notes.

> The top-level **Accommodations** page in the nav is *not* where you set rooms up — it's the member-facing screen for searching and requesting a place to stay (see §4).

### Setting up equipment

> **Access:** 🟠 Manager +

- **Equipment → Create**. Name the item and, if it lives at a particular property (say, kayaks at the lake house), assign it there — otherwise leave it as shared/group-wide. Search and filter the list by property.

## 4. Events: planning, signing up & reporting

> **Access:** 🟢 Everyone to view · 🔵 Member + to take part · 🟡 Coordinator + to plan

### Finding and viewing events

> **Access:** 🟢 Everyone

- **Events** lists upcoming gatherings; switch between calendar and list views. Open one to see its details and signup tabs.

### Signing up

> **Access:** 🔵 Member + (for yourself, or *Household Manager* for your household)

Open an event to reach three tabs. A household selector at the top lets managers act for any household; members work within their own.

- **Attendance** — tap **Going / Maybe / Not going** per person, per day. Going or Maybe reveals that day's meals for meal-by-meal RSVP; Not going clears them. The first time a household has no attendance yet, a short **Attendance Wizard** helps fill it in.
- **Tasks** — volunteer for a task on a given day and time slot; a coverage badge shows sign-ups versus how many are needed.
- **Request a place to stay** — from the event's Accommodations tab (or the **Accommodations** page), set your dates and party size, **Search**, and **Request** a spot. Your request moves through **Requested → Hold → Confirmed** (or **Declined**). Pending and confirmed stays show under **My upcoming stays** on the Dashboard.

### Viewing reports

> **Access:** 🔵 Member +

- **Reports** rolls an event's signups into a **per-day summary** — headcounts per meal, task coverage, accommodation occupancy, and the group's combined dietary needs, so cooks know how much and what kind of food to make. A print-friendly view opens your browser's print dialog. There's also a "View report" link on each event page.

### Creating and editing events

> **Access:** 🟡 Coordinator +

- **Create an event:** Events → **Create**. Choose the **property** it's held at (you need at least one first), give it a **name** and **date range**, and add notes.
- **Plan meals & tasks:** open the event and **Edit** it for the **Meals** and **Tasks** tabs. Meal templates pick which meals to plan (Breakfast/Lunch/Dinner) and generate a plan per day; a meal can optionally spin up a matching prep task. Task templates cover chores by time slot (Morning/Midday/Evening/Anytime) with an optional minimum number of volunteers. Either can be scoped to just part of the event.

### Arbitrating stay requests

> **Access:** 🟠 Manager +

- On an accommodation's stay page, **Promote** or **Decline** members' requests to move them along the Requested → Hold → Confirmed lifecycle.

## 5. Shopping list

> **Access:** 🟢 Everyone to view & claim · 🟡 Coordinator + to edit

The **Shopping** page turns headcounts and meal plans into a checklist anyone can pitch in on.

### Viewing and claiming items (shop mode)

> **Access:** 🟢 Everyone

1. Pick a **list** from the "View list" dropdown — either an **event** (which bundles the property's staples plus a section per planned meal) or a **property**. The list you're viewing is saved in the page's web address, so you can bookmark or share it.
2. Items are grouped by where they come from (Meal / Event / Property) and sorted by when they're needed.
3. On each item, tap **Claim** to pledge you'll bring it, **Got it** once you have it, or **Undo**. Enter a quantity so several people can cover one item between them — its status moves Needed → Claimed → Covered as pledges come in.
4. **Filter** by date (with a "Show past" toggle), by source, or to **unfulfilled only**. The list refreshes itself periodically and warns you if it's gone stale.

Your personal **My shopping** list — everything you've committed to — appears here and on the Dashboard.

### Editing the list (edit mode)

> **Access:** 🟡 Coordinator +

- Switch to **edit mode** to add, edit, and remove items (name, quantity, unit, needed-by date, category, notes). Meal items keep the meal's date; claimed or covered item names lock (delete and recreate to rename).

## 6. Everyday extras

> **Access:** 🟢 Everyone

- **Dashboard** — your home base: upcoming events (calendar or list) plus "My upcoming" widgets for your stays, tasks, and shopping.
- **Switch groups** — if you belong to more than one family group, use the account menu → **Switch group**.
- **Language** — toggle between English and Spanish anytime.
- **Feedback** — actions confirm with a brief on-screen message; there's no separate notifications inbox, so check the Dashboard for what needs your attention.
