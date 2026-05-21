interface SkillTagProps {
  skill: string;
}

function SkillTag({ skill }: SkillTagProps) {
  return <span className="skill-tag">{skill}</span>;
}

export default SkillTag;
