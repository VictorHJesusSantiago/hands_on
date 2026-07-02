-- Consulta pedida no desafio. Datas são parâmetros para manter segurança e testabilidade.
SELECT
    id,
    policy_number,
    insured_document,
    vehicle_plate,
    monthly_premium,
    coverage_start_date,
    coverage_end_date,
    status,
    created_at,
    updated_at
FROM policies
WHERE status = 1
  AND date(coverage_end_date) BETWEEN date(@today) AND date(@today, '+30 days')
ORDER BY date(coverage_end_date) ASC, policy_number ASC;
