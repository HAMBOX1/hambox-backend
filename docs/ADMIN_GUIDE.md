# HAMBOX admin guide

Everyday tasks for store staff. Menu names are the ones in the back office sidebar.

## Orders

**Orders → open an order.**

| I want to… | Do this | Needs |
|---|---|---|
| Change an order's status | _Change status_ → pick status → **write a reason** → Confirm | `Orders.Edit` |
| Refund | _Refund_ → reason → Confirm. If the customer paid through a gateway, the order shows **Awaiting gateway refund** — return the money in the gateway dashboard, then record the gateway reference under _Refund settlement_ | `Orders.Refund` |
| Give a code by hand | ⋯ menu → _Assign manual code_ → item, code, **reason** | `Orders.Edit` |
| Mark as delivered outside the system | ⋯ → _Mark as delivered (manual)_ → **reason and evidence** (chat/ticket reference). Only for paid orders | `Orders.Edit` |
| Write to the customer | ⋯ → _Contact customer_ — opens a support ticket for that order | `Orders.Edit` |
| Park an order | _Hold_ → reason. Nothing is delivered and no supplier is called until you _Release hold_ | `Orders.Edit` |
| Re-send codes / retry delivery | ⋯ → _Resend codes_ / _Retry fulfillment_ | `Orders.Edit` |
| Export the order's log | ⋯ → _Export order log (CSV)_ | `Orders.View` |

Bulk actions (order list → select rows): _Processing_, _Complete_, _Cancel_ and _Hold_ ask for **one reason**
that is written to every selected order. _Resend codes_ needs none.

Every manual action is in the order's **Audit** list with who, when, reason, old → new value, and IP.

## Customers' codes

Customers see codes only in **My Codes**. E-mails contain a link, never the code. An admin can reveal a key
(`Orders.RevealLicenseKeys`) but must give a reason, which is logged.

## Referrals

**Referrals.** A referral is **held for review** when several sign-ups come from one IP address, or one referrer
gets more than 5 in 24 hours. Held rows show a red _Held for review_ badge and earn nothing. Open the ⋯ menu →
**Approve** to release it (it is rewarded at the referred user's first completed order), or _Void_ to cancel it.

## Reviews

**Reviews.** Filter _Visible_ / _Hidden_. **Hide** needs a reason (kept with who and when) and removes the review
from the storefront; **Restore** puts it back. Helpful / not-helpful counts are shown per review.

## Logs and monitoring

- **Operations → Activity log** — search all audit trails; filter by source, entity (paste an order id), dates;
  _Export CSV_.
- **Operations → Dashboard / Jobs / Failed jobs** — queue health. Failed jobs can be retried or cancelled.
- **Operations → API logs** — every supplier call (secrets redacted).
- Critical alerts are e-mailed to the addresses in `Monitoring:AlertEmails`.

## Settings you will use

| Settings → | What it controls |
|---|---|
| Commerce | tax rate, reservation window, **orders/hour and failed payments/day** that put an order on hold |
| Retry policies | job retries and schedule, **supplier requests/second and 429 pause** |
| Currency | base currency, supported currencies, static rates override |
| SEO | default title/description, share image, robots (`noindex` hides the whole site), canonical domain |
| Analytics | GA4 / GTM / Meta Pixel ids and the master switch. Tools load only after the visitor accepts the cookie notice |
| Email | SMTP, sender. Scheduled reports are e-mailed as attachments through this |

Per-product purchase limits (max per order / per customer per day, manual review) are on each product's
**Purchase controls** panel.

## Support

Customers can open a ticket from their account, or use **Chat with support on WhatsApp** on an order page — it
opens WhatsApp with the order number already typed. Clicks are logged (`SupportWhatsAppClicked` in the activity
log). The WhatsApp number is the footer's WhatsApp link (Settings → Storefront → Footer).

## Legal pages

**Legal.** Terms, Privacy, Refund, Digital Delivery (require acceptance at checkout) and Support policy are
seeded in English and Arabic. Editing a published policy creates a **new version**; customers are asked to accept
the new version at their next checkout. Replace the `[Placeholder …]` paragraphs (governing law, data
retention, response times) with your confirmed text.
