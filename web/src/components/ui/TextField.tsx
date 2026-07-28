import { forwardRef, useId, type InputHTMLAttributes } from 'react';

interface TextFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  etiket: string;
  hata?: string;
  ipucu?: string;
}

/**
 * Form alani.
 *
 * Etiket gorsel olarak degil, <c>htmlFor</c> ile alana baglanir; ekran
 * okuyucu alanin ne oldugunu boyle ogrenir. Hata metni <c>aria-describedby</c>
 * ile iliskilendirilir ki odak alana geldiginde hata da okunsun.
 */
export const TextField = forwardRef<HTMLInputElement, TextFieldProps>(
  function TextField({ etiket, hata, ipucu, id, className = '', ...props }, ref) {
    const uretilenId = useId();
    const alanId = id ?? uretilenId;
    const hataId = `${alanId}-hata`;
    const ipucuId = `${alanId}-ipucu`;

    const aciklamalar = [hata ? hataId : null, ipucu ? ipucuId : null]
      .filter(Boolean)
      .join(' ');

    return (
      <div className="flex flex-col gap-base">
        <label
          htmlFor={alanId}
          className="font-body text-body-sm text-on-surface-variant"
        >
          {etiket}
        </label>

        <input
          {...props}
          id={alanId}
          ref={ref}
          aria-invalid={hata ? true : undefined}
          aria-describedby={aciklamalar || undefined}
          className={`
            w-full rounded-md bg-surface-container-low px-stack-sm py-stack-sm
            font-body text-body-md text-on-surface
            border transition-colors
            placeholder:text-on-surface-variant/50
            ${hata ? 'border-error' : 'border-outline-variant hover:border-outline'}
            ${className}
          `}
        />

        {ipucu && !hata && (
          <p id={ipucuId} className="font-body text-body-sm text-on-surface-variant/70">
            {ipucu}
          </p>
        )}

        {hata && (
          <p id={hataId} role="alert" className="font-body text-body-sm text-error">
            {hata}
          </p>
        )}
      </div>
    );
  },
);
