import React from "react";
import clsx from "clsx";
import styles from "./styles.module.css";

type FeatureItem = {
    title: string;
    Svg: React.ComponentType<React.ComponentProps<"svg">>;
    description: JSX.Element;
};

const FeatureList: FeatureItem[] = [
    {
        title: "Easy to Use",
        Svg: require("@site/static/img/undraw_tutorial_video_re_wepc.svg")
            .default,
        description: (
            <>
                CF-MC-Server is a simple CLI (
                <code>command line interface</code>) to help you install modpack
                servers by simple commands
            </>
        ),
    },
    {
        title: "Get Started Quickly",
        Svg: require("@site/static/img/undraw_completing_re_i7ap.svg").default,
        description: (
            <>
                With the interactive installer, you can easily search and
                install modpack servers within minutes!
            </>
        ),
    },
];

function Feature({ title, Svg, description }: FeatureItem) {
    return (
        <div className={clsx("col col--6")}>
            <div className="text--center">
                <Svg className={styles.featureSvg} role="img" />
            </div>
            <div className="text--center padding-horiz--md">
                <h3>{title}</h3>
                <p>{description}</p>
            </div>
        </div>
    );
}

export default function HomepageFeatures(): JSX.Element {
    return (
        <section className={styles.features}>
            <div className="container">
                <div className="row">
                    {FeatureList.map((props, idx) => (
                        <Feature key={idx} {...props} />
                    ))}
                </div>
            </div>
        </section>
    );
}
