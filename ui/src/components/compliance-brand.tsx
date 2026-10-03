import { Link } from '@askrjs/askr/router';
import { Brand, BrandLabel, BrandMark } from '@askrjs/themes/components';

export function ComplianceBrand() {
  return (
    <Brand asChild>
      <Link class="compliance-brand" href="/" aria-label="Badgers home">
        <BrandMark class="compliance-brand__mark" aria-hidden="true">
          <img
            class="compliance-brand__image"
            src="/brand/bdgrz-mark.png"
            alt=""
            width="32"
            height="32"
          />
        </BrandMark>
        <BrandLabel>Badgers - The Compliance Platform</BrandLabel>
      </Link>
    </Brand>
  );
}
