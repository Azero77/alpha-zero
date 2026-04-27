## Design Context

### Users
Admin/Teachers managing e-learning academies in multi-tenant environments. They need to orchestrate complex pipelines (video, assessments, course structures) with precision and confidence, often in low-bandwidth contexts.

### Brand Personality
- **Voice**: Professional, authoritative, yet approachable.
- **Tone**: Precise, efficient, and sophisticated.
- **3-Word Personality**: Clean, Controlled, Performant.
- **Emotional Goals**: Absolute Control ("I am managing a complex machine with ease").

### Aesthetic Direction
- **Visual Tone**: Stripe/Vercel style—refined minimalism with high-precision typography and intentional whitespace.
- **References**: Vercel Dashboard, Stripe Dashboard.
- **Anti-references**: Generic "admin template" grids, heavy drop shadows, excessive rounded corners, glowing neon accents.
- **Theme**: Light-first with a high-contrast Dark mode. Focus on structural clarity.

### Design Principles
1. **Precision Typography**: Use font weight and size (Inter) to create hierarchy without relying on boxes or colors.
2. **Lean Assets**: Favor CSS-only styling and SVG icons (Lucide) over heavy image assets to reflect the platform's low-bandwidth roots.
3. **State Visibility**: Async operations (like transcoding) must be visually tracked with elegant, non-obtrusive indicators.
4. **Structural Isolation**: Use clear boundaries and consistent spacing to represent the underlying modular architecture (Orchestrator vs. Provider).
5. **Contextual Intelligence**: All Resource ARNs and metadata snapshots should be accessible but not overwhelming.
